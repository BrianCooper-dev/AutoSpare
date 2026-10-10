namespace AutoSpare.Application.Sales.DTOs;

public class SaleDetailsDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit => TotalAmount - TotalCost;

    public List<SaleDetailsItemDto> Items { get; set; } = new();
}

public class SaleDetailsItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public string? ImageUrl { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitPurchasePrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;
    public decimal TotalCost => Quantity * UnitPurchasePrice;
    public decimal Profit => TotalPrice - TotalCost;
}
