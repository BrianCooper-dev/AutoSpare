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
    public string InvoiceNumber { private set; get; } = string.Empty;

    /// <summary>
    /// شناسه تأمین‌کننده
    /// </summary>
    public Guid SupplierId { private set; get; }
    public Supplier? Supplier { private set; get; }

    /// <summary>
    /// شناسه انباری که کالاها به آن واریز می‌شوند
    /// </summary>
    public Guid WarehouseId { private set; get; }
    public Warehouse? Warehouse { private set; get; }

    /// <summary>
    /// تاریخ و زمان انجام خرید
    /// </summary>
    public DateTime PurchaseDate { private set; get; }

    /// <summary>
    /// وضعیت فاکتور خرید
    /// </summary>
    public PurchaseStatus Status { private set; get; }

    /// <summary>
    /// توضیحات یا یادداشت فاکتور
    /// </summary>
    public string? Notes { private set; get; }

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
            throw new ArgumentException("شناسه انبار نامعتبر است.", nameof(warehouseId));

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
    /// افزودن یا به‌روزرسانی آیتم در فاکتور (تنها در حالت Draft)
    /// </summary>
    public void AddOrUpdateItem(Guid productId, int quantity, decimal unitPrice)
    {
        EnsureIsDraft();

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existingItem != null)
        {
            existingItem.Update(quantity, unitPrice);
        }
        else
        {
            _items.Add(new PurchaseItem(productId, quantity, unitPrice));
        }
    }

    /// <summary>
    /// حذف یک آیتم از فاکتور
    /// </summary>
    public void RemoveItem(Guid productId)
    {
        EnsureIsDraft();

        var item = _items.FirstOrDefault(i => i.ProductId == productId);
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
    /// لغو فاکتور خرید
    /// </summary>
    public void Cancel()
    {
        if (Status == PurchaseStatus.Completed)
            throw new InvalidOperationException("فاکتور نهایی‌شده را نمی‌توان لغو کرد.");

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

