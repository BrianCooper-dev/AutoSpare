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
        if (!dto.CategoryId.HasValue)
            throw new InvalidOperationException("دسته‌بندی الزامی است.");

        if (!dto.BrandId.HasValue)
            throw new InvalidOperationException("برند الزامی است.");

        // ۱. ایجاد کالا بر اساس Domain Aggregate
        var product = new Product(
            name: dto.Name?.Trim() ?? string.Empty,
            internalCode: dto.InternalCode?.Trim() ?? string.Empty,
            model: dto.Model?.Trim() ?? string.Empty,
            purchasePrice: dto.PurchasePrice,
            salePrice: dto.SalePrice,
            categoryId: dto.CategoryId.Value,
            brandId: dto.BrandId.Value,
            imagePath: null,
            defaultWarehouseId: dto.DefaultWarehouseId
        );

        await _context.Products.AddAsync(product);

        // ۲. در صورت تعیین انبار، ثبت موجودی و گردش اولیه در کاردکس
        if (dto.DefaultWarehouseId.HasValue && dto.DefaultWarehouseId.Value != Guid.Empty)
        {
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
}
