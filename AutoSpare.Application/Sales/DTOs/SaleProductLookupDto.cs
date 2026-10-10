namespace AutoSpare.Application.Sales.DTOs;

public record SaleProductLookupDto(
    Guid Id,
    string Name,
    string InternalCode,
    string? Model,
    string? BrandName,
    decimal PurchasePrice,
    decimal SalePrice,
    string? ImagePath,
    Dictionary<Guid, int> StocksByWarehouse,
    Guid? DefaultWarehouseId,
    int TotalStock
)
{
    public string DisplayText =>
        $"{Name} ({BrandName ?? "-"}) - کد: {InternalCode} | موجودی کل: {TotalStock} | فروش: {SalePrice:N0}";
}
