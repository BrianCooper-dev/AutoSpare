using AutoSpare.Application.Products.DTOs;

namespace AutoSpare.Application.Products;

public interface IProductService
{
    Task<List<DropdownItemDto>> GetCategoriesLookupAsync();
    Task<List<DropdownItemDto>> GetBrandsLookupAsync();
    Task<List<DropdownItemDto>> GetWarehousesLookupAsync();
    Task<bool> IsInternalCodeUniqueAsync(string code);
    Task<Guid> CreateProductAsync(CreateProductDto dto);
    Task<List<ProductListItemDto>> GetProductsAsync(string? searchTerm = null);
}
