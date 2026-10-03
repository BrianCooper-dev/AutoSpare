using AutoSpare.Application.Products;
using AutoSpare.Application.Products.DTOs;
using AutoSpare.Domain.Inventories;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Products.Enums;
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

        var isUnique = await IsInternalCodeUniqueAsync(dto.InternalCode);
        if (!isUnique)
            throw new InvalidOperationException($"کد فنی '{dto.InternalCode}' قبلاً در سیستم ثبت شده است.");

        if (dto.InitialQuantity < 0)
            throw new ArgumentException("تعداد موجودی اولیه نمی‌تواند منفی باشد.", nameof(dto));

        if (dto.InitialQuantity > 0 && !dto.DefaultWarehouseId.HasValue)
            throw new InvalidOperationException("در صورت تعیین موجودی اولیه، انتخاب انبار الزامی است.");

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

        if (dto.InitialQuantity > 0 && dto.DefaultWarehouseId.HasValue)
        {
            var warehouseExists = await _context.Warehouses
                .AnyAsync(w => w.Id == dto.DefaultWarehouseId.Value);

            if (!warehouseExists)
                throw new InvalidOperationException("انبار انتخاب شده در سیستم یافت نشد.");

            var inventory = new Inventory(
                productId: product.Id,
                warehouseId: dto.DefaultWarehouseId.Value,
                initialQuantity: dto.InitialQuantity
            );

            await _context.Inventories.AddAsync(inventory);
        }

        await _context.SaveChangesAsync();
        return product.Id;
    }

    public async Task<List<ProductListItemDto>> GetProductsAsync(ProductFilterDto? filter = null)
    {
        filter ??= new ProductFilterDto();

        var query = _context.Products
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(p =>
                p.Name.Contains(term) ||
                p.InternalCode.Contains(term) ||
                p.Model.Contains(term) ||
                (p.Brand != null && p.Brand.Name.Contains(term)));
        }

        if (filter.CategoryId.HasValue && filter.CategoryId.Value != Guid.Empty)
        {
            query = query.Where(p => p.CategoryId == filter.CategoryId.Value);
        }

        if (filter.BrandId.HasValue && filter.BrandId.Value != Guid.Empty)
        {
            query = query.Where(p => p.BrandId == filter.BrandId.Value);
        }

        if (filter.WarehouseId.HasValue && filter.WarehouseId.Value != Guid.Empty)
        {
            var warehouseId = filter.WarehouseId.Value;
            query = query.Where(p =>
                p.DefaultWarehouseId == warehouseId ||
                _context.Inventories.Any(i => i.ProductId == p.Id && i.WarehouseId == warehouseId));
        }

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
                Status = p.Status,
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

        if (product == null) return null;

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

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == dto.Id);
        if (product == null)
            throw new InvalidOperationException("کالای مورد نظر یافت نشد.");

        var isUnique = await IsInternalCodeUniqueAsync(dto.InternalCode, dto.Id);
        if (!isUnique)
            throw new InvalidOperationException($"کد فنی '{dto.InternalCode}' قبلاً برای کالای دیگری ثبت شده است.");

        product.UpdateDetails(
            name: dto.Name.Trim(),
            internalCode: dto.InternalCode.Trim(),
            model: dto.Model?.Trim() ?? string.Empty,
            categoryId: dto.CategoryId.Value,
            brandId: dto.BrandId.Value,
            imagePath: dto.ImagePath
        );

        product.UpdatePrices(dto.PurchasePrice, dto.SalePrice);
        product.SetDefaultWarehouse(dto.DefaultWarehouseId);

        await _context.SaveChangesAsync();
    }

    public async Task ToggleProductStatusAsync(Guid id)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null)
            throw new InvalidOperationException("کالای مورد نظر یافت نشد.");

        var newStatus = product.Status == ProductStatus.Active
            ? ProductStatus.Inactive
            : ProductStatus.Active;

        product.ChangeStatus(newStatus);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteProductAsync(Guid id)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null)
            throw new InvalidOperationException("کالای مورد نظر یافت نشد.");

        // بررسی وجود هرگونه سابقه گردش موجودی (کاردکس)
        var hasStockMovements = await _context.StockMovements.AnyAsync(sm => sm.ProductId == id);

        // بررسی وجود موجودی در انبارها
        var totalStock = await _context.Inventories
            .Where(i => i.ProductId == id)
            .SumAsync(i => (int?)i.Quantity) ?? 0;

        if (hasStockMovements || totalStock > 0)
        {
            throw new InvalidOperationException(
                "این کالا دارای گردش کاردکس یا موجودی در انبار است و امکان حذف فیزیکی آن وجود ندارد. لطفاً به‌جای حذف، آن را غیرفعال کنید.");
        }

        // حذف رکوردهای خالی مربوطه در صورت وجود
        var inventories = await _context.Inventories.Where(i => i.ProductId == id).ToListAsync();
        if (inventories.Count > 0)
        {
            _context.Inventories.RemoveRange(inventories);
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
    }
}
