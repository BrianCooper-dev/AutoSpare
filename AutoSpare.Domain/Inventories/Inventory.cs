using AutoSpare.Domain.Common;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Warehouses;

namespace AutoSpare.Domain.Inventories;

public class Inventory : BaseEntity
{
    public Guid ProductId { get; private set; }
    public Product? Product { get; private set; }

    public Guid WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; private set; }

    public int Quantity { get; private set; }

    // سازنده برای EF Core
    protected Inventory()
    {
    }

    public Inventory(Guid productId, Guid warehouseId, int initialQuantity = 0)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("شناسه محصول نامعتبر است.", nameof(productId));

        if (warehouseId == Guid.Empty)
            throw new ArgumentException("شناسه انبار نامعتبر است.", nameof(warehouseId));

        ProductId = productId;
        WarehouseId = warehouseId;
        SetQuantity(initialQuantity);
    }

    // متد افزایش موجودی (مثلاً هنگام خرید یا مرجوعی)
    public void IncreaseQuantity(int amount)
    {
        if (amount <= 0)
            throw new ArgumentException("مقدار افزایش باید بیشتر از صفر باشد.", nameof(amount));

        Quantity += amount;
        UpdateModificationTime();
    }

    // متد کاهش موجودی (مثلاً هنگام فروش)
    public void DecreaseQuantity(int amount)
    {
        if (amount <= 0)
            throw new ArgumentException("مقدار کاهش باید بیشتر از صفر باشد.", nameof(amount));

        if (Quantity - amount < 0)
            throw new InvalidOperationException("موجودی کافی نیست.");

        Quantity -= amount;
        UpdateModificationTime();
    }

    // متد تنظیم مستقیم موجودی (برای انبارگردانی)
    public void AdjustQuantity(int newQuantity)
    {
        SetQuantity(newQuantity);
        UpdateModificationTime();
    }

    private void SetQuantity(int quantity)
    {
        if (quantity < 0)
            throw new ArgumentException("موجودی نمی‌تواند منفی باشد.", nameof(quantity));

        Quantity = quantity;
    }
}
