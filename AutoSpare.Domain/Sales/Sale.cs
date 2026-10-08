using AutoSpare.Domain.Common;
using AutoSpare.Domain.Sales.Enums;
using AutoSpare.Domain.Warehouses;

namespace AutoSpare.Domain.Sales;

public class Sale : BaseEntity
{
    private readonly List<SaleItem> _items = new();

    public string InvoiceNumber { private set; get; } = string.Empty;
    public string CustomerName { private set; get; } = string.Empty;

    /// <summary>انبار پیش‌فرض فاکتور (اختیاری - انبار در سطح هر ردیف تعیین می‌شود)</summary>
    public Guid? WarehouseId { private set; get; }
    public Warehouse? Warehouse { private set; get; }

    public DateTime SaleDate { private set; get; }
    public SaleStatus Status { private set; get; }
    public string? Notes { private set; get; }
    public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();
    public decimal TotalAmount => _items.Sum(item => item.TotalPrice);

    // سازنده مخصوص EF Core
    private Sale() { }

    public Sale(
        string invoiceNumber,
        string customerName,
        Guid? warehouseId = null,
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

    public void AddOrUpdateItem(Guid productId, Guid warehouseId, int quantity, decimal unitPrice)
    {
        EnsureIsDraft();

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId && i.WarehouseId == warehouseId);
        if (existingItem != null)
            existingItem.Update(warehouseId, quantity, unitPrice);
        else
            _items.Add(new SaleItem(productId, warehouseId, quantity, unitPrice));
    }

    public void RemoveItem(Guid productId, Guid warehouseId)
    {
        EnsureIsDraft();
        var item = _items.FirstOrDefault(i => i.ProductId == productId && i.WarehouseId == warehouseId);
        if (item != null) _items.Remove(item);
    }

    public void Complete()
    {
        EnsureIsDraft();
        if (!_items.Any())
            throw new InvalidOperationException("نمی‌توان فاکتور فروش بدون کالا را نهایی کرد.");
        Status = SaleStatus.Completed;
    }

    public void Cancel()
    {
        if (Status == SaleStatus.Completed)
            throw new InvalidOperationException("فاکتور نهایی‌شده را نمی‌توان لغو کرد.");
        Status = SaleStatus.Cancelled;
    }

    public void UpdateHeader(string invoiceNumber, string customerName, Guid? warehouseId, DateTime saleDate, string? notes)
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
