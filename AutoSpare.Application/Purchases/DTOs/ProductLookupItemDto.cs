namespace AutoSpare.Application.Purchases.DTOs;

public record ProductLookupItemDto(
    Guid Id,
    string Name,
    string InternalCode,
    string? Model = null,
    string? BrandName = null,
    decimal DefaultPurchasePrice = 0m,
    string? ImagePath = null
)
{
    public string DisplayText => $"{Name} ({BrandName ?? "-"}) - {InternalCode}";
}
