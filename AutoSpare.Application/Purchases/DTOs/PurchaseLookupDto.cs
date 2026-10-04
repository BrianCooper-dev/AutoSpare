namespace AutoSpare.Application.Purchases.DTOs;

public record ProductLookupItemDto(Guid Id, string Name, string InternalCode, decimal DefaultPurchasePrice);

