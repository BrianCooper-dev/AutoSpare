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

    public async Task<bool> IsInternalCodeUniqueAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return true;
        var trimmed = code.Trim();
        return !await _context.Products.AnyAsync(p => p.InternalCode == trimmed);
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
            imagePath: null,
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
}
