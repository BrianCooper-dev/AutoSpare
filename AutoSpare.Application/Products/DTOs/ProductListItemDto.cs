namespace AutoSpare.Application.Products.DTOs;

public class ProductListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string InternalCode { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string? DefaultWarehouseName { get; set; }
    public decimal SalePrice { get; set; }
    public decimal PurchasePrice { get; set; }
    public int TotalStock { get; set; } // مجموع موجودی در تمام انبارها
}
