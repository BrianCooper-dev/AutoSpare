using AutoSpare.Application.Products.DTOs;
using AutoSpare.Application.Sales.DTOs;

namespace AutoSpare.Application.Sales;

public interface ISaleService
{
    Task<List<DropdownItemDto>> GetWarehousesLookupAsync();
    Task<List<SaleProductLookupDto>> GetProductsForSaleLookupAsync(); // ← بدون warehouseId
    Task<string> GenerateInvoiceNumberAsync();
    Task<SaleDetailsDto?> GetSaleDetailsAsync(Guid saleId);
    Task<Guid> CreateSaleAsync(CreateSaleDto dto, string? currentUserName = null);
}
