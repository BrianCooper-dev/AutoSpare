namespace AutoSpare.Application.Products.DTOs;

public class LowStockAlertDto
{
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string InternalCode { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int MinStockThreshold { get; set; }
    public string? ImagePath { get; set; }

    // فیلد کمکی محاسبه کسری مورد نیاز تا رسیدن به سقف هشدار
    public int Deficit => MinStockThreshold > CurrentStock ? MinStockThreshold - CurrentStock : 0;
}
