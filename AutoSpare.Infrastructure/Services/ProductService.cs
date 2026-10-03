using AutoSpare.Application.Products;
using AutoSpare.Application.Products.DTOs;
using AutoSpare.Domain.Inventories;
using AutoSpare.Domain.Products;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _context;

    public ProductService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DropdownItemDto>> GetCategoriesLookupAsync() =>
        await _context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new DropdownItemDto(c.Id, c.Name))
            .ToListAsync();

    public async Task<List<DropdownItemDto>> GetBrandsLookupAsync() =>
        await _context.Brands
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new DropdownItemDto(b.Id, b.Name))
            .ToListAsync();

    public async Task<List<DropdownItemDto>> GetWarehousesLookupAsync() =>
        await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.IsActive)
            .Select(w => new DropdownItemDto(w.Id, w.Name))
            .ToListAsync();

    public async Task<bool> IsInternalCodeUniqueAsync(string code, Guid? currentProductId = null)
    {
        if (string.IsNullOrWhiteSpace(code)) return true;
        var trimmed = code.Trim();

        return !await _context.Products
            .AnyAsync(p => p.InternalCode == trimmed && (!currentProductId.HasValue || p.Id != currentProductId.Value));
    }


    public async Task<Guid> CreateProductAsync(CreateProductDto dto)
    {
        // ۱. اعتبارسنجی مقادیر پایه کالا
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("نام کالا الزامی است.", nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.InternalCode))
            throw new ArgumentException("کد فنی کالا الزامی است.", nameof(dto));

        if (!dto.CategoryId.HasValue || dto.CategoryId.Value == Guid.Empty)
            throw new ArgumentException("انتخاب دسته‌بندی الزامی است.", nameof(dto));

        if (!dto.BrandId.HasValue || dto.BrandId.Value == Guid.Empty)
            throw new ArgumentException("انتخاب برند الزامی است.", nameof(dto));

        if (dto.PurchasePrice < 0)
            throw new ArgumentException("قیمت خرید نمی‌تواند منفی باشد.", nameof(dto));

        if (dto.SalePrice < dto.PurchasePrice)
            throw new ArgumentException("قیمت فروش نمی‌تواند کمتر از قیمت خرید باشد.", nameof(dto));

        // ۲. اعتبارسنجی یکتایی کد فنی
        var isUnique = await IsInternalCodeUniqueAsync(dto.InternalCode);
        if (!isUnique)
            throw new InvalidOperationException($"کد فنی '{dto.InternalCode}' قبلاً در سیستم ثبت شده است.");

        // ۳. اعتبارسنجی بیزینس رول موجودی اولیه
        if (dto.InitialQuantity < 0)
            throw new ArgumentException("تعداد موجودی اولیه نمی‌تواند منفی باشد.", nameof(dto));

        if (dto.InitialQuantity > 0 && !dto.DefaultWarehouseId.HasValue)
            throw new InvalidOperationException("در صورت تعیین موجودی اولیه، انتخاب انبار الزامی است.");


        // ۴. ساخت انتیتی کالا بر اساس سازنده دامین
        var product = new Product(
            name: dto.Name.Trim(),
            internalCode: dto.InternalCode.Trim(),
            model: dto.Model?.Trim() ?? string.Empty,
            purchasePrice: dto.PurchasePrice,
            salePrice: dto.SalePrice,
            categoryId: dto.CategoryId.Value,
            brandId: dto.BrandId.Value,
            imagePath: dto.ImagePath,
            defaultWarehouseId: dto.DefaultWarehouseId
        );


        await _context.Products.AddAsync(product);

        // ۵. ثبت موجودی اولیه (Inventory و StockMovement)
        if (dto.InitialQuantity > 0 && dto.DefaultWarehouseId.HasValue)
        {
            var warehouseExists = await _context.Warehouses
                .AnyAsync(w => w.Id == dto.DefaultWarehouseId.Value);

            if (!warehouseExists)
                throw new InvalidOperationException("انبار انتخاب شده در سیستم یافت نشد.");

            // سازنده Inventory خود به صورت خودکار حرکت Initial در StockMovement را ثبت می‌کند
            var inventory = new Inventory(
                productId: product.Id,
                warehouseId: dto.DefaultWarehouseId.Value,
                initialQuantity: dto.InitialQuantity
            );

            await _context.Inventories.AddAsync(inventory);
        }

        // ۶. ذخیره تمامی تغییرات در یک تراکنش اتمیک واحد دیتابیس
        await _context.SaveChangesAsync();

        return product.Id;
    }

    public async Task<List<ProductListItemDto>> GetProductsAsync(ProductFilterDto? filter = null)
    {
        filter ??= new ProductFilterDto();

        var query = _context.Products
            .AsNoTracking()
            .AsQueryable();

        // ۱. فیلتر جست‌وجوی متنی (نام، کد فنی، مدل، برند)
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(p =>
                p.Name.Contains(term) ||
                p.InternalCode.Contains(term) ||
                p.Model.Contains(term) ||
                (p.Brand != null && p.Brand.Name.Contains(term)));
        }

        // ۲. فیلتر دسته‌بندی
        if (filter.CategoryId.HasValue && filter.CategoryId.Value != Guid.Empty)
        {
            query = query.Where(p => p.CategoryId == filter.CategoryId.Value);
        }

        // ۳. فیلتر برند
        if (filter.BrandId.HasValue && filter.BrandId.Value != Guid.Empty)
        {
            query = query.Where(p => p.BrandId == filter.BrandId.Value);
        }

        // ۴. فیلتر انبار (کالاهایی که در این انبار ثبت شده‌اند یا انبار پیش‌فرضشان این است)
        if (filter.WarehouseId.HasValue && filter.WarehouseId.Value != Guid.Empty)
        {
            var warehouseId = filter.WarehouseId.Value;
            query = query.Where(p =>
                p.DefaultWarehouseId == warehouseId ||
                _context.Inventories.Any(i => i.ProductId == p.Id && i.WarehouseId == warehouseId));
        }

        // ۵. پروجکشن داده‌ها به DTO به همراه محاسبه موجودی
        var projectedQuery = query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProductListItemDto
            {
                Id = p.Id,
                Name = p.Name,
                InternalCode = p.InternalCode,
                Model = p.Model,
                ImagePath = p.ImagePath,
                SalePrice = p.SalePrice,
                PurchasePrice = p.PurchasePrice,
                CategoryName = p.Category != null ? p.Category.Name : "-",
                BrandName = p.Brand != null ? p.Brand.Name : "-",
                DefaultWarehouseName = p.DefaultWarehouse != null ? p.DefaultWarehouse.Name : null,
                TotalStock = filter.WarehouseId.HasValue
                    ? (_context.Inventories
                        .Where(i => i.ProductId == p.Id && i.WarehouseId == filter.WarehouseId.Value)
                        .Sum(i => (int?)i.Quantity) ?? 0)
                    : (_context.Inventories
                        .Where(i => i.ProductId == p.Id)
                        .Sum(i => (int?)i.Quantity) ?? 0)
            });

        // ۶. فیلتر وضعیت موجودی (موجود / ناموجود / کم‌موجودی)
        switch (filter.StockStatus)
        {
            case StockStatusFilter.InStock:
                projectedQuery = projectedQuery.Where(p => p.TotalStock > 0);
                break;

            case StockStatusFilter.OutOfStock:
                projectedQuery = projectedQuery.Where(p => p.TotalStock == 0);
                break;

            case StockStatusFilter.LowStock:
                projectedQuery =
                    projectedQuery.Where(p => p.TotalStock > 0 && p.TotalStock <= filter.LowStockThreshold);
                break;

            case StockStatusFilter.All:
            default:
                break;
        }

        return await projectedQuery.ToListAsync();
    }

    public async Task<ProductDetailsDto?> GetProductDetailsByIdAsync(Guid id)
    {
        var product = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.DefaultWarehouse)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return null;
        }

        // ۱. دریافت موجودی در انبارها
        var warehouseStocks = await _context.Inventories
            .AsNoTracking()
            .Where(i => i.ProductId == id)
            .Select(i => new ProductWarehouseStockDto
            {
                WarehouseId = i.WarehouseId,
                WarehouseName = _context.Warehouses
                    .Where(w => w.Id == i.WarehouseId)
                    .Select(w => w.Name)
                    .FirstOrDefault() ?? "نامشخص",
                Quantity = i.Quantity
            })
            .ToListAsync();

        // ۲. دریافت تاریخچه گردش موجودی (کاردکس)
        var movements = await _context.StockMovements
            .AsNoTracking()
            .Where(sm => sm.ProductId == id)
            .OrderByDescending(sm => sm.OccurredAt)
            .Select(sm => new StockMovementDto
            {
                Id = sm.Id,
                WarehouseName = _context.Warehouses
                    .Where(w => w.Id == sm.WarehouseId)
                    .Select(w => w.Name)
                    .FirstOrDefault() ?? "نامشخص",
                Type = sm.Type,
                QuantityChange = sm.QuantityChange,
                BalanceAfter = sm.BalanceAfter,
                Reference = sm.Reference,
                Reason = sm.Reason,
                PerformedBy = sm.PerformedBy,
                OccurredAt = sm.OccurredAt
            })
            .ToListAsync();

        return new ProductDetailsDto
        {
            Id = product.Id,
            Name = product.Name,
            InternalCode = product.InternalCode,
            Model = product.Model,
            ImagePath = product.ImagePath,
            PurchasePrice = product.PurchasePrice,
            SalePrice = product.SalePrice,
            CategoryName = product.Category != null ? product.Category.Name : "-",
            BrandName = product.Brand != null ? product.Brand.Name : "-",
            DefaultWarehouseName = product.DefaultWarehouse != null ? product.DefaultWarehouse.Name : "-",
            TotalStock = warehouseStocks.Sum(ws => ws.Quantity),
            CreatedAt = product.CreatedAt,
            WarehouseStocks = warehouseStocks,
            Movements = movements
        };
    }

    public async Task<UpdateProductDto?> GetProductForEditByIdAsync(Guid id)
    {
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null) return null;

        return new UpdateProductDto
        {
            Id = product.Id,
            Name = product.Name,
            InternalCode = product.InternalCode,
            Model = product.Model,
            PurchasePrice = product.PurchasePrice,
            SalePrice = product.SalePrice,
            CategoryId = product.CategoryId,
            BrandId = product.BrandId,
            DefaultWarehouseId = product.DefaultWarehouseId,
            ImagePath = product.ImagePath
        };
    }

    public async Task UpdateProductAsync(UpdateProductDto dto)
    {
        // ۱. اعتبارسنجی مقادیر پایه
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("نام کالا الزامی است.", nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.InternalCode))
            throw new ArgumentException("کد فنی کالا الزامی است.", nameof(dto));

        if (!dto.CategoryId.HasValue || dto.CategoryId.Value == Guid.Empty)
            throw new ArgumentException("انتخاب دسته‌بندی الزامی است.", nameof(dto));

        if (!dto.BrandId.HasValue || dto.BrandId.Value == Guid.Empty)
            throw new ArgumentException("انتخاب برند الزامی است.", nameof(dto));

        if (dto.PurchasePrice < 0)
            throw new ArgumentException("قیمت خرید نمی‌تواند منفی باشد.", nameof(dto));

        if (dto.SalePrice < dto.PurchasePrice)
            throw new ArgumentException("قیمت فروش نمی‌تواند کمتر از قیمت خرید باشد.", nameof(dto));

        // ۲. واکشی انتیتی کالا
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == dto.Id);
        if (product == null)
            throw new InvalidOperationException("کالای مورد نظر یافت نشد.");

        // ۳. بررسی یکتایی کد فنی (با در نظر نگرفتن شناسه خود کالا)
        var isUnique = await IsInternalCodeUniqueAsync(dto.InternalCode, dto.Id);
        if (!isUnique)
            throw new InvalidOperationException($"کد فنی '{dto.InternalCode}' قبلاً برای کالای دیگری ثبت شده است.");

        // ۴. به‌روزرسانی مشخصات پایه و ارتباطات
        product.UpdateDetails(
            name: dto.Name.Trim(),
            internalCode: dto.InternalCode.Trim(),
            model: dto.Model?.Trim() ?? string.Empty,
            categoryId: dto.CategoryId.Value,
            brandId: dto.BrandId.Value,
            imagePath: dto.ImagePath
        );

        // ۵. به‌روزرسانی قیمت‌ها (بدون تغییر در موجودی‌ها و کاردکس قبلی)
        product.UpdatePrices(dto.PurchasePrice, dto.SalePrice);

        // ۶. به‌روزرسانی انبار پیش‌فرض
        product.SetDefaultWarehouse(dto.DefaultWarehouseId);

        // ۷. ذخیره نهایی
        await _context.SaveChangesAsync();
    }
}
