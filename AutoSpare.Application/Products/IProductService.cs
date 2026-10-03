using AutoSpare.Application.Products.DTOs;

namespace AutoSpare.Application.Products;

public interface IProductService
{
    Task<List<DropdownItemDto>> GetCategoriesLookupAsync();
    Task<List<DropdownItemDto>> GetBrandsLookupAsync();
    Task<List<DropdownItemDto>> GetWarehousesLookupAsync();
    Task<bool> IsInternalCodeUniqueAsync(string code, Guid? currentProductId = null);
    Task<Guid> CreateProductAsync(CreateProductDto dto);
    Task<UpdateProductDto?> GetProductForEditByIdAsync(Guid id);
    Task UpdateProductAsync(UpdateProductDto dto);
    Task<List<ProductListItemDto>> GetProductsAsync(ProductFilterDto? filter = null);
    Task<ProductDetailsDto?> GetProductDetailsByIdAsync(Guid id);
    Task ToggleProductStatusAsync(Guid id);
    Task DeleteProductAsync(Guid id);
}
