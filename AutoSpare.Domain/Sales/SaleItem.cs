using AutoSpare.Domain.Common;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Warehouses;

namespace AutoSpare.Domain.Sales;

public class SaleItem : BaseEntity
{
    public Guid SaleId { private set; get; }

    public Guid ProductId { private set; get; }
    public Product? Product { private set; get; }

    /// <summary>انباری که این ردیف کالا از آن خارج می‌شود</summary>
    public Guid WarehouseId { private set; get; }

    public Warehouse? Warehouse { private set; get; }

    public int Quantity { private set; get; }

    /// <summary>قیمت واحد فروش در لحظه ثبت (Snapshot)</summary>
    public decimal UnitPrice { private set; get; }

    /// <summary>قیمت واحد خرید کالا در لحظه فروش (Snapshot برای محاسبه سود)</summary>
    public decimal UnitPurchasePrice { private set; get; }

    public decimal TotalPrice => Quantity * UnitPrice;
    public decimal TotalCost => Quantity * UnitPurchasePrice;
    public decimal Profit => TotalPrice - TotalCost;

    private SaleItem() { }

    internal SaleItem(Guid productId, Guid warehouseId, int quantity, decimal unitPrice, decimal unitPurchasePrice = 0m)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("شناسه محصول نامعتبر است.", nameof(productId));
        if (warehouseId == Guid.Empty)
            throw new ArgumentException("شناسه انبار نامعتبر است.", nameof(warehouseId));

        SetQuantity(quantity);
        SetUnitPrice(unitPrice);
        SetUnitPurchasePrice(unitPurchasePrice);

        ProductId = productId;
        WarehouseId = warehouseId;
    }

    internal void Update(Guid warehouseId, int quantity, decimal unitPrice, decimal unitPurchasePrice = 0m)
    {
        if (warehouseId == Guid.Empty)
            throw new ArgumentException("شناسه انبار نامعتبر است.", nameof(warehouseId));

        WarehouseId = warehouseId;
        SetQuantity(quantity);
        SetUnitPrice(unitPrice);
        if (unitPurchasePrice > 0m)
        {
            SetUnitPurchasePrice(unitPurchasePrice);
        }
    }

    private void SetQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("تعداد فروخته‌شده باید بزرگتر از صفر باشد.", nameof(quantity));
        Quantity = quantity;
    }

    private void SetUnitPrice(decimal unitPrice)
    {
        if (unitPrice < 0)
            throw new ArgumentException("قیمت واحد فروش نمی‌تواند منفی باشد.", nameof(unitPrice));
        UnitPrice = unitPrice;
    }

    private void SetUnitPurchasePrice(decimal unitPurchasePrice)
    {
        if (unitPurchasePrice < 0)
            throw new ArgumentException("قیمت واحد خرید نمی‌تواند منفی باشد.", nameof(unitPurchasePrice));
        UnitPurchasePrice = unitPurchasePrice;
    }
}
