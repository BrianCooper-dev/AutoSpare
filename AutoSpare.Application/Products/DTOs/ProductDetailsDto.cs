using AutoSpare.Domain.Inventories.Enums;

namespace AutoSpare.Application.Products.DTOs;

public class ProductDetailsDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string InternalCode { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string? DefaultWarehouseName { get; set; }
    public int TotalStock { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<ProductWarehouseStockDto> WarehouseStocks { get; set; } = new();
    public List<StockMovementDto> Movements { get; set; } = new();
}

public class ProductWarehouseStockDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class StockMovementDto
{
    public Guid Id { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public StockMovementType Type { get; set; }
    public int QuantityChange { get; set; }
    public int BalanceAfter { get; set; }
    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public string? PerformedBy { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
