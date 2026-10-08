namespace AutoSpare.Application.Sales.DTOs;


public record SaleProductLookupDto(
    Guid Id,
    string Name,
    string InternalCode,
    string? Model,
    string? BrandName,
    decimal SalePrice,
    string? ImagePath,
    Dictionary<Guid, int> StocksByWarehouse,   // موجودی در هر انبار
    Guid? DefaultWarehouseId,                  // انبار با بیشترین موجودی
    int TotalStock
)
{
    public string DisplayText => $"{Name} ({BrandName ?? "-"}) - کد: {InternalCode} | موجودی کل: {TotalStock}";
}

