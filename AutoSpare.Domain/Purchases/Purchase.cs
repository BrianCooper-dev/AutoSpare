using AutoSpare.Domain.Common;
using AutoSpare.Domain.Purchases.Enums;
using AutoSpare.Domain.Suppliers;
using AutoSpare.Domain.Warehouses;

namespace AutoSpare.Domain.Purchases;

/// <summary>
/// سربرگ فاکتور خرید از تأمین‌کننده
/// </summary>
public class Purchase : BaseEntity
{
    private readonly List<PurchaseItem> _items = new();

    /// <summary>
    /// شماره فاکتور خرید/سند
    /// </summary>
    public string InvoiceNumber { get; private set; } = string.Empty;

    /// <summary>
    /// شناسه تأمین‌کننده
    /// </summary>
    public Guid SupplierId { get; private set; }
    public Supplier? Supplier { get; private set; }

    /// <summary>
    /// انبار پیش‌فرض فاکتور (برای ردیف‌هایی که انبار خاصی تعیین نکرده‌اند)
    /// </summary>
    public Guid WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; private set; }

    /// <summary>
    /// تاریخ و زمان انجام خرید
    /// </summary>
    public DateTime PurchaseDate { get; private set; }

    /// <summary>
    /// وضعیت فاکتور خرید
    /// </summary>
    public PurchaseStatus Status { get; private set; }

    /// <summary>
    /// توضیحات یا یادداشت فاکتور
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// لیست ردیف‌های کالاها (ReadOnly)
    /// </summary>
    public IReadOnlyCollection<PurchaseItem> Items => _items.AsReadOnly();

    /// <summary>
    /// مبلغ کل فاکتور خرید
    /// </summary>
    public decimal TotalAmount => _items.Sum(item => item.TotalPrice);

    // سازنده مخصوص EF Core
    private Purchase() { }

    public Purchase(
        string invoiceNumber,
        Guid supplierId,
        Guid warehouseId,
        DateTime? purchaseDate = null,
        string? notes = null)
    {
        SetInvoiceNumber(invoiceNumber);

        if (supplierId == Guid.Empty)
            throw new ArgumentException("شناسه تأمین‌کننده نامعتبر است.", nameof(supplierId));

        if (warehouseId == Guid.Empty)
            throw new ArgumentException("شناسه انبار پیش‌فرض نامعتبر است.", nameof(warehouseId));

        SupplierId = supplierId;
        WarehouseId = warehouseId;
        PurchaseDate = purchaseDate ?? DateTime.UtcNow;
        Status = PurchaseStatus.Draft;
        Notes = notes?.Trim();
    }

    // ==========================================
    // Domain Behaviors / Business Methods
    // ==========================================

    /// <summary>
    /// افزودن یا به‌روزرسانی آیتم در فاکتور بر اساس ترکیب کالا و انبار مقصد
    /// </summary>
    public void AddOrUpdateItem(Guid productId, Guid warehouseId, int quantity, decimal unitPrice)
    {
        EnsureIsDraft();

        if (warehouseId == Guid.Empty)
            warehouseId = WarehouseId; // در صورت خالی بودن، انبار پیش‌فرض هدر ست می‌شود

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId && i.WarehouseId == warehouseId);
        if (existingItem != null)
        {
            existingItem.Update(existingItem.Quantity + quantity, unitPrice);
        }
        else
        {
            _items.Add(new PurchaseItem(productId, warehouseId, quantity, unitPrice));
        }
    }

    /// <summary>
    /// حذف یک آیتم مشخص از فاکتور با کالا و انبار
    /// </summary>
    public void RemoveItem(Guid productId, Guid warehouseId)
    {
        EnsureIsDraft();

        var item = _items.FirstOrDefault(i => i.ProductId == productId && i.WarehouseId == warehouseId);
        if (item != null)
        {
            _items.Remove(item);
        }
    }

    /// <summary>
    /// نهایی‌سازی فاکتور خرید
    /// </summary>
    public void Complete()
    {
        EnsureIsDraft();

        if (!_items.Any())
            throw new InvalidOperationException("نمی‌توان فاکتور خرید بدون کالا را نهایی کرد.");

        Status = PurchaseStatus.Completed;
    }

    /// <summary>
    /// لغو فاکتور خرید (از حالت پیش‌نویس یا نهایی)
    /// </summary>
    public void Cancel()
    {
        if (Status == PurchaseStatus.Cancelled)
            throw new InvalidOperationException("این فاکتور قبلاً لغو شده است.");

        Status = PurchaseStatus.Cancelled;
    }

    /// <summary>
    /// ویرایش اطلاعات اصلی فاکتور
    /// </summary>
    public void UpdateHeader(string invoiceNumber, Guid supplierId, Guid warehouseId, DateTime purchaseDate, string? notes)
    {
        EnsureIsDraft();

        SetInvoiceNumber(invoiceNumber);

        if (supplierId == Guid.Empty)
            throw new ArgumentException("شناسه تأمین‌کننده نامعتبر است.", nameof(supplierId));

        if (warehouseId == Guid.Empty)
            throw new ArgumentException("شناسه انبار نامعتبر است.", nameof(warehouseId));

        SupplierId = supplierId;
        WarehouseId = warehouseId;
        PurchaseDate = purchaseDate;
        Notes = notes?.Trim();
    }

    // ==========================================
    // Helpers
    // ==========================================

    private void SetInvoiceNumber(string invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("شماره فاکتور خرید نمی‌تواند خالی باشد.", nameof(invoiceNumber));

        InvoiceNumber = invoiceNumber.Trim();
    }

    private void EnsureIsDraft()
    {
        if (Status != PurchaseStatus.Draft)
            throw new InvalidOperationException("تغییرات فقط در حالت پیش‌نویس (Draft) امکان‌پذیر است.");
    }
}
