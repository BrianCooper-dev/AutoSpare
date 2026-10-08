namespace AutoSpare.Application.Sales.DTOs;

public class CreateSaleItemDto
{
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? InternalCode { get; set; }
    public string? BrandName { get; set; }
    public Guid? WarehouseId { get; set; }
    public int AvailableStock { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal? UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * (UnitPrice ?? 0m);
}
