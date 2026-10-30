using AutoSpare.Domain.Common;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Warehouses;

namespace AutoSpare.Domain.Purchases;

/// <summary>
/// ردیف کالای خریداری‌شده در فاکتور خرید همراه با انبار مقصد مجزا
/// </summary>
public class PurchaseItem : BaseEntity
{
    public Guid PurchaseId { get; private set; }

    public Guid ProductId { get; private set; }
    public Product? Product { get; private set; }

    /// <summary>
    /// شناسه انبار مقصد این ردیف کالا
    /// </summary>
    public Guid WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; private set; }

    /// <summary>
    /// تعداد خریداری شده
    /// </summary>
    public int Quantity { get; private set; }

    /// <summary>
    /// قیمت فی (واحد) خرید
    /// </summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>
    /// جمع کل این ردیف (تعداد * قیمت واحد)
    /// </summary>
    public decimal TotalPrice => Quantity * UnitPrice;

    // سازنده مخصوص EF Core
    private PurchaseItem() { }

    internal PurchaseItem(Guid productId, Guid warehouseId, int quantity, decimal unitPrice)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("شناسه محصول نامعتبر است.", nameof(productId));

        if (warehouseId == Guid.Empty)
            throw new ArgumentException("شناسه انبار نامعتبر است.", nameof(warehouseId));

        ProductId = productId;
        WarehouseId = warehouseId;
        SetQuantity(quantity);
        SetUnitPrice(unitPrice);
    }

    internal void Update(int quantity, decimal unitPrice)
    {
        SetQuantity(quantity);
        SetUnitPrice(unitPrice);
    }

    internal void ChangeWarehouse(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
            throw new ArgumentException("شناسه انبار نامعتبر است.", nameof(warehouseId));

        WarehouseId = warehouseId;
    }

    private void SetQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("تعداد خریداری‌شده باید بزرگتر از صفر باشد.", nameof(quantity));

        Quantity = quantity;
    }

    private void SetUnitPrice(decimal unitPrice)
    {
        if (unitPrice < 0)
            throw new ArgumentException("قیمت واحد نمی‌تواند منفی باشد.", nameof(unitPrice));

        UnitPrice = unitPrice;
    }
}
