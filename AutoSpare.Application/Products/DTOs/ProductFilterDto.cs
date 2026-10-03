namespace AutoSpare.Application.Products.DTOs;

public enum StockStatusFilter
{
    All = 0, // همه
    InStock = 1, // موجود (بزرگتر از 0)
    OutOfStock = 2, // ناموجود (برابر با 0)
    LowStock = 3 // کم‌موجودی (بین 1 تا 5)
}

public class ProductFilterDto
{
    public string? SearchTerm { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? WarehouseId { get; set; }
    public StockStatusFilter StockStatus { get; set; } = StockStatusFilter.All;

    // حد آستانه کم‌موجودی (پیش‌فرض ۵ عدد)
    public int LowStockThreshold { get; set; } = 5;
}
