namespace AutoSpare.Domain.Products.Enums;

public enum ProductStatus
{
    Active = 1,      // موجود و قابل فروش
    Inactive = 2,    // غیرفعال (بدون نمایش در لیست فروش)
    OutOfStock = 3,  // اتمام موجودی (خودکار یا دستی)
    Discontinued = 4 // تولید یا عرضه نشده
}
