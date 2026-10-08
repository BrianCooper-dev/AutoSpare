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
    public decimal UnitPrice { private set; get; }
    public decimal TotalPrice => Quantity * UnitPrice;

    private SaleItem() { }

    internal SaleItem(Guid productId, Guid warehouseId, int quantity, decimal unitPrice)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("شناسه محصول نامعتبر است.", nameof(productId));
        if (warehouseId == Guid.Empty)
            throw new ArgumentException("شناسه انبار نامعتبر است.", nameof(warehouseId));

        SetQuantity(quantity);
        SetUnitPrice(unitPrice);

        ProductId = productId;
        WarehouseId = warehouseId;
    }

    internal void Update(Guid warehouseId, int quantity, decimal unitPrice)
    {
        if (warehouseId == Guid.Empty)
            throw new ArgumentException("شناسه انبار نامعتبر است.", nameof(warehouseId));

        WarehouseId = warehouseId;
        SetQuantity(quantity);
        SetUnitPrice(unitPrice);
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
}
