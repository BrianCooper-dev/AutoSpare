using AutoSpare.Application.Common.Models;
using AutoSpare.Application.Products.DTOs;

namespace AutoSpare.Application.Products;

public interface IProductService
{
    Task<List<DropdownItemDto>> GetCategoriesLookupAsync(CancellationToken cancellationToken = default);
    Task<List<DropdownItemDto>> GetBrandsLookupAsync(CancellationToken cancellationToken = default);
    Task<List<DropdownItemDto>> GetWarehousesLookupAsync(CancellationToken cancellationToken = default);
    Task<bool> IsInternalCodeUniqueAsync(string code, Guid? currentProductId = null, CancellationToken cancellationToken = default);
    Task<Guid> CreateProductAsync(CreateProductDto dto, CancellationToken cancellationToken = default);
    Task<UpdateProductDto?> GetProductForEditByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateProductAsync(UpdateProductDto dto, CancellationToken cancellationToken = default);
    Task<ProductDetailsDto?> GetProductDetailsByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task ToggleProductStatusAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<LowStockAlertDto>> GetLowStockAlertsAsync(Guid? warehouseId = null, CancellationToken cancellationToken = default);
    Task<List<LowStockAlertDto>> GetTopLowStockAlertsAsync(int count = 5, CancellationToken cancellationToken = default);
    Task AdjustProductsPriceAsync(BatchPriceAdjustmentDto dto, CancellationToken cancellationToken = default);
    Task<PagedResult<ProductListItemDto>> GetProductsAsync(ProductFilterDto? filter = null, CancellationToken cancellationToken = default);
}
