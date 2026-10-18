namespace AutoSpare.Application.Reports.DTOs;

public class InventoryValuationDto
{
    public Guid ProductId { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal TotalPurchaseValue => CurrentStock * PurchasePrice;
    public decimal TotalSaleValue => CurrentStock * SalePrice;
    public decimal PotentialProfit => TotalSaleValue - TotalPurchaseValue;
}
