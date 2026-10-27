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

    private const int MaxNameLength = 200;
    private const int MaxInternalCodeLength = 50;
    private const int MaxModelLength = 150;
    private const int MaxImagePathLength = 500;

    public ProductService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DropdownItemDto>> GetCategoriesLookupAsync()
    {
        return await _context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new DropdownItemDto(c.Id, c.Name))
            .ToListAsync();
    }

    public async Task<List<DropdownItemDto>> GetBrandsLookupAsync()
    {
        return await _context.Brands
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new DropdownItemDto(b.Id, b.Name))
            .ToListAsync();
    }

    public async Task<List<DropdownItemDto>> GetWarehousesLookupAsync()
    {
        return await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.IsActive)
            .Select(w => new DropdownItemDto(w.Id, w.Name))
            .ToListAsync();
    }

    public async Task<bool> IsInternalCodeUniqueAsync(
        string code,
        Guid? currentProductId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return true;
        }

        var trimmed = code.Trim();

        return !await _context.Products.AnyAsync(p =>
            p.InternalCode == trimmed &&
            (!currentProductId.HasValue || p.Id != currentProductId.Value));
    }

    public async Task<Guid> CreateProductAsync(CreateProductDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        ValidateProductInput(
            name: dto.Name,
            internalCode: dto.InternalCode,
            model: dto.Model,
            imagePath: dto.ImagePath,
            categoryId: dto.CategoryId,
            brandId: dto.BrandId,
            purchasePrice: dto.PurchasePrice,
            salePrice: dto.SalePrice,
            isLowStockAlertEnabled: dto.IsLowStockAlertEnabled,
            minStockThreshold: dto.MinStockThreshold);

        if (dto.InitialQuantity < 0)
        {
            throw new ArgumentException(
                "موجودی اولیه نمی‌تواند منفی باشد.",
                nameof(dto));
        }

        var normalizedDefaultWarehouseId = NormalizeOptionalGuid(dto.DefaultWarehouseId);

        if (dto.InitialQuantity > 0 && !normalizedDefaultWarehouseId.HasValue)
        {
            throw new InvalidOperationException(
                "انتخاب انبار الزامی است.");
        }

        var isUnique = await IsInternalCodeUniqueAsync(dto.InternalCode);

        if (!isUnique)
        {
            throw new InvalidOperationException(
                $"کد فنی '{dto.InternalCode}' قبلاً در سیستم ثبت شده است.");
        }

        if (normalizedDefaultWarehouseId.HasValue)
        {
            var warehouseExists = await _context.Warehouses
                .AnyAsync(w => w.Id == normalizedDefaultWarehouseId.Value);

            if (!warehouseExists)
            {
                throw new InvalidOperationException(
                    "انبار انتخاب شده در سیستم یافت نشد.");
            }
        }

        var product = new Product(
            dto.Name.Trim(),
            dto.InternalCode.Trim(),
            dto.Model?.Trim(),
            dto.PurchasePrice,
            dto.SalePrice,
            dto.CategoryId!.Value,
            dto.BrandId!.Value,
            imagePath: dto.ImagePath?.Trim(),
            defaultWarehouseId: normalizedDefaultWarehouseId,
            isLowStockAlertEnabled: dto.IsLowStockAlertEnabled,
            minStockThreshold: dto.MinStockThreshold);

        await _context.Products.AddAsync(product);

        if (dto.InitialQuantity > 0 && normalizedDefaultWarehouseId.HasValue)
        {
            var inventory = new Inventory(
                productId: product.Id,
                warehouseId: normalizedDefaultWarehouseId.Value,
                initialQuantity: dto.InitialQuantity);

            await _context.Inventories.AddAsync(inventory);
        }

        await _context.SaveChangesAsync();

        return product.Id;
    }

    public async Task<List<ProductListItemDto>> GetProductsAsync(
        ProductFilterDto? filter = null)
    {
        filter ??= new ProductFilterDto();

        var hasWarehouseFilter =
            filter.WarehouseId.HasValue &&
            filter.WarehouseId.Value != Guid.Empty;

        var filteredWarehouseId = hasWarehouseFilter
            ? filter.WarehouseId!.Value
            : Guid.Empty;

        var query = _context.Products
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();

            query = query.Where(p =>
                p.Name.Contains(term) ||
                p.InternalCode.Contains(term) ||
                (p.Model != null && p.Model.Contains(term)) ||
                (p.Brand != null && p.Brand.Name.Contains(term)));
        }

        if (filter.CategoryId.HasValue &&
            filter.CategoryId.Value != Guid.Empty)
        {
            query = query.Where(p => p.CategoryId == filter.CategoryId.Value);
        }

        if (filter.BrandId.HasValue &&
            filter.BrandId.Value != Guid.Empty)
        {
            query = query.Where(p => p.BrandId == filter.BrandId.Value);
        }

        if (hasWarehouseFilter)
        {
            query = query.Where(p =>
                p.DefaultWarehouseId == filteredWarehouseId ||
                _context.Inventories.Any(i =>
                    i.ProductId == p.Id &&
                    i.WarehouseId == filteredWarehouseId));
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

                CategoryName = p.Category != null
                    ? p.Category.Name
                    : "-",

                BrandName = p.Brand != null
                    ? p.Brand.Name
                    : "-",

                DefaultWarehouseName = p.DefaultWarehouse != null
                    ? p.DefaultWarehouse.Name
                    : null,

                IsLowStockAlertEnabled = p.IsLowStockAlertEnabled,
                MinStockThreshold = p.MinStockThreshold,

                TotalStock = hasWarehouseFilter
                    ? (_context.Inventories
                        .Where(i =>
                            i.ProductId == p.Id &&
                            i.WarehouseId == filteredWarehouseId)
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
                projectedQuery = projectedQuery.Where(p =>
                    p.TotalStock > 0 &&
                    (p.IsLowStockAlertEnabled
                        ? p.TotalStock <= p.MinStockThreshold
                        : p.TotalStock <= filter.LowStockThreshold));
                break;

            case StockStatusFilter.All:
            default:
                break;
        }

        return await projectedQuery.ToListAsync();
    }

    public async Task<ProductDetailsDto?> GetProductDetailsByIdAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسه کالا نامعتبر است.",
                nameof(id));
        }

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

        var priceHistories = await _context.PriceChangeHistories
            .AsNoTracking()
            .Where(h => h.ProductId == id)
            .OrderByDescending(h => h.ChangeDate)
            .Select(h => new PriceChangeHistoryDto
            {
                Id = h.Id,
                OldPurchasePrice = h.OldPurchasePrice,
                NewPurchasePrice = h.NewPurchasePrice,
                OldSalePrice = h.OldSalePrice,
                NewSalePrice = h.NewSalePrice,
                ChangeType = h.ChangeType,
                ChangeDate = h.ChangeDate,
                Reason = h.Reason
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

            CategoryName = product.Category != null
                ? product.Category.Name
                : "-",

            BrandName = product.Brand != null
                ? product.Brand.Name
                : "-",

            DefaultWarehouseName = product.DefaultWarehouse != null
                ? product.DefaultWarehouse.Name
                : "-",

            TotalStock = warehouseStocks.Sum(ws => ws.Quantity),
            CreatedAt = product.CreatedAt,
            WarehouseStocks = warehouseStocks,
            Movements = movements,
            PriceHistories = priceHistories,
            IsLowStockAlertEnabled = product.IsLowStockAlertEnabled,
            MinStockThreshold = product.MinStockThreshold
        };
    }

    public async Task<UpdateProductDto?> GetProductForEditByIdAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسه کالا نامعتبر است.",
                nameof(id));
        }

        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return null;
        }

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
            ImagePath = product.ImagePath,
            IsLowStockAlertEnabled = product.IsLowStockAlertEnabled,
            MinStockThreshold = product.MinStockThreshold
        };
    }

    public async Task UpdateProductAsync(UpdateProductDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Id == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسه کالا نامعتبر است.",
                nameof(dto));
        }

        ValidateProductInput(
            name: dto.Name,
            internalCode: dto.InternalCode,
            model: dto.Model,
            imagePath: dto.ImagePath,
            categoryId: dto.CategoryId,
            brandId: dto.BrandId,
            purchasePrice: dto.PurchasePrice,
            salePrice: dto.SalePrice,
            isLowStockAlertEnabled: dto.IsLowStockAlertEnabled,
            minStockThreshold: dto.MinStockThreshold);

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == dto.Id);

        if (product == null)
        {
            throw new InvalidOperationException(
                "کالای مورد نظر یافت نشد.");
        }

        var isUnique = await IsInternalCodeUniqueAsync(
            dto.InternalCode,
            dto.Id);

        if (!isUnique)
        {
            throw new InvalidOperationException(
                $"کد فنی '{dto.InternalCode}' قبلاً برای کالای دیگری ثبت شده است.");
        }

        var normalizedDefaultWarehouseId = NormalizeOptionalGuid(dto.DefaultWarehouseId);

        if (normalizedDefaultWarehouseId.HasValue)
        {
            var warehouseExists = await _context.Warehouses
                .AnyAsync(w => w.Id == normalizedDefaultWarehouseId.Value);

            if (!warehouseExists)
            {
                throw new InvalidOperationException(
                    "انبار انتخاب شده در سیستم یافت نشد.");
            }
        }

        product.UpdateDetails(
            dto.Name.Trim(),
            dto.InternalCode.Trim(),
            dto.Model?.Trim(),
            dto.CategoryId!.Value,
            dto.BrandId!.Value,
            dto.ImagePath?.Trim());

        product.UpdatePrices(
            dto.PurchasePrice,
            dto.SalePrice);

        product.SetDefaultWarehouse(normalizedDefaultWarehouseId);

        product.SetLowStockAlert(
            dto.IsLowStockAlertEnabled,
            dto.MinStockThreshold);

        await _context.SaveChangesAsync();
    }

    public async Task ToggleProductStatusAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسه کالا نامعتبر است.",
                nameof(id));
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            throw new InvalidOperationException(
                "کالای مورد نظر یافت نشد.");
        }

        var newStatus = product.Status == ProductStatus.Active
            ? ProductStatus.Inactive
            : ProductStatus.Active;

        product.ChangeStatus(newStatus);

        await _context.SaveChangesAsync();
    }

    public async Task DeleteProductAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسه کالا نامعتبر است.",
                nameof(id));
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            throw new InvalidOperationException(
                "کالای مورد نظر یافت نشد.");
        }

        var hasStockMovements = await _context.StockMovements
            .AnyAsync(sm => sm.ProductId == id);

        var totalStock = await _context.Inventories
            .Where(i => i.ProductId == id)
            .SumAsync(i => (int?)i.Quantity) ?? 0;

        if (hasStockMovements || totalStock > 0)
        {
            throw new InvalidOperationException(
                "این کالا دارای گردش کاردکس یا موجودی در انبار است و امکان حذف فیزیکی آن وجود ندارد. لطفاً به‌جای حذف، آن را غیرفعال کنید.");
        }

        var inventories = await _context.Inventories
            .Where(i => i.ProductId == id)
            .ToListAsync();

        if (inventories.Count > 0)
        {
            _context.Inventories.RemoveRange(inventories);
        }

        _context.Products.Remove(product);

        await _context.SaveChangesAsync();
    }

    public async Task<List<LowStockAlertDto>> GetLowStockAlertsAsync(Guid? warehouseId = null)
    {
        var query = _context.Inventories
            .AsNoTracking()
            .Where(i =>
                i.Product != null &&
                i.Product.IsLowStockAlertEnabled &&
                i.Quantity <= i.Product.MinStockThreshold);

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            query = query.Where(i => i.WarehouseId == warehouseId.Value);
        }

        return await query
            .OrderBy(i => i.Quantity)
            .Select(i => new LowStockAlertDto
            {
                ProductId = i.ProductId,
                WarehouseId = i.WarehouseId,
                ProductName = i.Product!.Name,
                InternalCode = i.Product.InternalCode,
                Model = i.Product.Model,

                BrandName = i.Product.Brand != null
                    ? i.Product.Brand.Name
                    : "-",

                CategoryName = i.Product.Category != null
                    ? i.Product.Category.Name
                    : "-",

                WarehouseName = i.Warehouse != null
                    ? i.Warehouse.Name
                    : "-",

                CurrentStock = i.Quantity,
                MinStockThreshold = i.Product.MinStockThreshold,
                ImagePath = i.Product.ImagePath
            })
            .ToListAsync();
    }

    public async Task<List<LowStockAlertDto>> GetTopLowStockAlertsAsync(int count = 5)
    {
        if (count <= 0)
        {
            throw new ArgumentException(
                "تعداد باید بزرگتر از صفر باشد.",
                nameof(count));
        }

        return await _context.Inventories
            .AsNoTracking()
            .Where(i =>
                i.Product != null &&
                i.Product.IsLowStockAlertEnabled &&
                i.Quantity <= i.Product.MinStockThreshold)
            .OrderBy(i => i.Quantity)
            .Take(count)
            .Select(i => new LowStockAlertDto
            {
                ProductId = i.ProductId,
                WarehouseId = i.WarehouseId,
                ProductName = i.Product!.Name,
                InternalCode = i.Product.InternalCode,
                Model = i.Product.Model,

                BrandName = i.Product.Brand != null
                    ? i.Product.Brand.Name
                    : "-",

                CategoryName = i.Product.Category != null
                    ? i.Product.Category.Name
                    : "-",

                WarehouseName = i.Warehouse != null
                    ? i.Warehouse.Name
                    : "-",

                CurrentStock = i.Quantity,
                MinStockThreshold = i.Product.MinStockThreshold,
                ImagePath = i.Product.ImagePath
            })
            .ToListAsync();
    }

    public async Task AdjustProductsPriceAsync(BatchPriceAdjustmentDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Items == null || dto.Items.Count == 0)
        {
            throw new InvalidOperationException(
                "هیچ کالایی برای تغییر قیمت انتخاب نشده است.");
        }

        if (dto.Items.Any(i => i.ProductId == Guid.Empty))
        {
            throw new ArgumentException(
                "شناسه یکی از کالاهای انتخابی نامعتبر است.",
                nameof(dto));
        }

        var productIds = dto.Items
            .Select(i => i.ProductId)
            .Distinct()
            .ToList();

        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        if (products.Count != productIds.Count)
        {
            throw new InvalidOperationException(
                "برخی از کالاهای انتخابی در سیستم یافت نشدند.");
        }

        var historyRecords = new List<PriceChangeHistory>();
        var changeDate = DateTime.UtcNow;

        foreach (var item in dto.Items)
        {
            var product = products.First(p => p.Id == item.ProductId);

            if (product.SalePrice == item.NewSalePrice)
            {
                continue;
            }

            if (item.NewSalePrice < product.PurchasePrice)
            {
                throw new InvalidOperationException(
                    "قیمت فروش جدید نمی‌تواند کمتر از قیمت خرید باشد.");
            }

            var oldSalePrice = product.SalePrice;
            var oldPurchasePrice = product.PurchasePrice;

            product.UpdatePrices(
                oldPurchasePrice,
                item.NewSalePrice);

            var history = new PriceChangeHistory(
                productId: product.Id,
                oldPurchasePrice: oldPurchasePrice,
                newPurchasePrice: oldPurchasePrice,
                oldSalePrice: oldSalePrice,
                newSalePrice: item.NewSalePrice,
                changeType: dto.ChangeType,
                reason: dto.Reason,
                changeDate: changeDate);

            historyRecords.Add(history);
        }

        if (historyRecords.Count > 0)
        {
            await _context.PriceChangeHistories.AddRangeAsync(historyRecords);
            await _context.SaveChangesAsync();
        }
    }

    private static void ValidateProductInput(
        string? name,
        string? internalCode,
        string? model,
        string? imagePath,
        Guid? categoryId,
        Guid? brandId,
        decimal purchasePrice,
        decimal salePrice,
        bool isLowStockAlertEnabled,
        int minStockThreshold)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "نام کالا الزامی است.");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"نام کالا نمی‌تواند بیش از {MaxNameLength} کاراکتر باشد.");
        }

        if (string.IsNullOrWhiteSpace(internalCode))
        {
            throw new ArgumentException(
                "کد فنی کالا الزامی است.");
        }

        if (internalCode.Trim().Length > MaxInternalCodeLength)
        {
            throw new ArgumentException(
                $"کد فنی نمی‌تواند بیش از {MaxInternalCodeLength} کاراکتر باشد.");
        }

        if (!string.IsNullOrWhiteSpace(model) &&
            model.Trim().Length > MaxModelLength)
        {
            throw new ArgumentException(
                $"مدل کالا نمی‌تواند بیش از {MaxModelLength} کاراکتر باشد.");
        }

        if (!string.IsNullOrWhiteSpace(imagePath) &&
            imagePath.Trim().Length > MaxImagePathLength)
        {
            throw new ArgumentException(
                $"مسیر تصویر نمی‌تواند بیش از {MaxImagePathLength} کاراکتر باشد.");
        }

        if (!categoryId.HasValue || categoryId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "انتخاب دسته‌بندی الزامی است.");
        }

        if (!brandId.HasValue || brandId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "انتخاب برند الزامی است.");
        }

        if (purchasePrice < 0)
        {
            throw new ArgumentException(
                "قیمت خرید نمی‌تواند منفی باشد.");
        }

        if (salePrice < 0)
        {
            throw new ArgumentException(
                "قیمت فروش نمی‌تواند منفی باشد.");
        }

        if (salePrice < purchasePrice)
        {
            throw new ArgumentException(
                "قیمت فروش نمی‌تواند کمتر از قیمت خرید باشد.");
        }

        if (minStockThreshold < 0)
        {
            throw new ArgumentException(
                "آستانه هشدار نمی‌تواند منفی باشد.");
        }

        _ = isLowStockAlertEnabled;
    }

    private static Guid? NormalizeOptionalGuid(Guid? value)
    {
        return value.HasValue && value.Value != Guid.Empty
            ? value.Value
            : null;
    }
}
