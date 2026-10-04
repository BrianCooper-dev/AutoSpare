namespace AutoSpare.Application.Purchases.DTOs;

public class CreatePurchaseDto
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime? PurchaseDate { get; set; } = DateTime.Today;
    public string? Notes { get; set; }

    /// <summary>
    /// آیا فاکتور در لحظه ثبت مستقیماً نهایی (Completed) شده و انبار شارژ شود؟
    /// </summary>
    public bool FinalizeImmediately { get; set; } = true;

    public List<CreatePurchaseItemDto> Items { get; set; } = new();
}

public class CreatePurchaseItemDto
{
    public Guid? ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;
}
