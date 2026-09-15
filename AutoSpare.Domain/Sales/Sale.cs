using AutoSpare.Domain.Common;
using AutoSpare.Domain.Sales.Enums;
using AutoSpare.Domain.Warehouses;

namespace AutoSpare.Domain.Sales;

/// <summary>
/// سربرگ فاکتور فروش کالا
/// </summary>
public class Sale : BaseEntity
{
    private readonly List<SaleItem> _items = new();

    /// <summary>
    /// شماره فاکتور فروش
    /// </summary>
    public string InvoiceNumber { private set; get; } = string.Empty;

    /// <summary>
    /// نام مشتری یا خریدار
    /// </summary>
    public string CustomerName { private set; get; } = string.Empty;

    /// <summary>
    /// شناسه انباری که کالاها از آن خارج می‌شوند
    /// </summary>
    public Guid WarehouseId { private set; get; }
    public Warehouse? Warehouse { private set; get; }

    /// <summary>
    /// تاریخ و زمان فروش
    /// </summary>
    public DateTime SaleDate { private set; get; }

    /// <summary>
    /// وضعیت فاکتور فروش
    /// </summary>
    public SaleStatus Status { private set; get; }

    /// <summary>
    /// توضیحات یا یادداشت فاکتور
    /// </summary>
    public string? Notes { private set; get; }

    /// <summary>
    /// لیست ردیف‌های کالاها (ReadOnly)
    /// </summary>
    public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();

    /// <summary>
    /// مبلغ کل فاکتور فروش
    /// </summary>
    public decimal TotalAmount => _items.Sum(item => item.TotalPrice);

    // سازنده مخصوص EF Core
    private Sale() { }

    public Sale(
        string invoiceNumber,
        string customerName,
        Guid warehouseId,
        DateTime? saleDate = null,
        string? notes = null)
    {
        SetInvoiceNumber(invoiceNumber);
        SetCustomerName(customerName);

        if (warehouseId == Guid.Empty)
            throw new ArgumentException("شناسه انبار نامعتبر است.", nameof(warehouseId));

        WarehouseId = warehouseId;
        SaleDate = saleDate ?? DateTime.UtcNow;
        Status = SaleStatus.Draft;
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
            _items.Add(new SaleItem(productId, quantity, unitPrice));
        }
    }

    /// <summary>
    /// حذف یک آیتم از فاکتور فروش
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
    /// نهایی‌سازی فاکتور فروش
    /// </summary>
    public void Complete()
    {
        EnsureIsDraft();

        if (!_items.Any())
            throw new InvalidOperationException("نمی‌توان فاکتور فروش بدون کالا را نهایی کرد.");

        Status = SaleStatus.Completed;
    }

    /// <summary>
    /// لغو فاکتور فروش
    /// </summary>
    public void Cancel()
    {
        if (Status == SaleStatus.Completed)
            throw new InvalidOperationException("فاکتور نهایی‌شده را نمی‌توان لغو کرد.");

        Status = SaleStatus.Cancelled;
    }

    /// <summary>
    /// ویرایش اطلاعات اصلی سربرگ فاکتور فروش
    /// </summary>
    public void UpdateHeader(string invoiceNumber, string customerName, Guid warehouseId, DateTime saleDate, string? notes)
    {
        EnsureIsDraft();

        SetInvoiceNumber(invoiceNumber);
        SetCustomerName(customerName);

        if (warehouseId == Guid.Empty)
            throw new ArgumentException("شناسه انبار نامعتبر است.", nameof(warehouseId));

        WarehouseId = warehouseId;
        SaleDate = saleDate;
        Notes = notes?.Trim();
    }

    // ==========================================
    // Helpers
    // ==========================================

    private void SetInvoiceNumber(string invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("شماره فاکتور فروش نمی‌تواند خالی باشد.", nameof(invoiceNumber));

        InvoiceNumber = invoiceNumber.Trim();
    }

    private void SetCustomerName(string customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("نام مشتری نمی‌تواند خالی باشد.", nameof(customerName));

        CustomerName = customerName.Trim();
    }

    private void EnsureIsDraft()
    {
        if (Status != SaleStatus.Draft)
            throw new InvalidOperationException("تغییرات فقط در حالت پیش‌نویس (Draft) امکان‌پذیر است.");
    }
}

