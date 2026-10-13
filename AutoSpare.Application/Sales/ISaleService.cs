using AutoSpare.Application.Products.DTOs;
using AutoSpare.Application.Sales.DTOs;

namespace AutoSpare.Application.Sales;

public interface ISaleService
{
    Task<List<SaleListDto>> GetSalesAsync(
        string? search = null,
        DateTime? fromDate = null,
        DateTime? toDate = null);

    Task<List<DropdownItemDto>> GetWarehousesLookupAsync();
    Task<List<SaleProductLookupDto>> GetProductsForSaleLookupAsync();
    Task<string> GenerateInvoiceNumberAsync();
    Task<SaleDetailsDto?> GetSaleDetailsAsync(Guid saleId);
    Task<Guid> CreateSaleAsync(CreateSaleDto dto, string? currentUserName = null);
    Task<bool> FinalizeSaleAsync(Guid saleId, CancellationToken cancellationToken = default);
    Task CancelSaleAsync(Guid saleId, string? reason = null);
}
