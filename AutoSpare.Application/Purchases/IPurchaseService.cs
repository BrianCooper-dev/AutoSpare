using AutoSpare.Application.Products.DTOs;
using AutoSpare.Application.Purchases.DTOs;

namespace AutoSpare.Application.Purchases;

public interface IPurchaseService
{
    Task<List<DropdownItemDto>> GetSuppliersLookupAsync();
    Task<List<DropdownItemDto>> GetWarehousesLookupAsync();
    Task<List<ProductLookupItemDto>> GetProductsLookupAsync();
    Task<Guid> CreatePurchaseAsync(CreatePurchaseDto dto, string? currentUserName = null);
    Task<List<PurchaseListDto>> GetPurchasesAsync();
    Task<PurchaseDetailsDto?> GetPurchaseDetailsByIdAsync(Guid id);
    Task<string> GenerateInvoiceNumberAsync();
}
