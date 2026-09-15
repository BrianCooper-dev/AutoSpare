using AutoSpare.Domain.Common;
using AutoSpare.Domain.Inventories.Enums;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Warehouses;

namespace AutoSpare.Domain.Inventories;

public class Inventory : BaseEntity
{
    private readonly List<StockMovement> _movements = new();

    public Guid ProductId { get; private set; }
    public Product? Product { get; private set; }

    public Guid WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>تاریخچهٔ کامل تغییرات این موجودی (فقط خواندنی)</summary>
    public virtual IReadOnlyCollection<StockMovement> Movements => _movements.AsReadOnly();

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

        if (initialQuantity < 0)
            throw new ArgumentException("موجودی اولیه نمی‌تواند منفی باشد.", nameof(initialQuantity));

        ProductId = productId;
        WarehouseId = warehouseId;
        Quantity = 0;

        // موجودی اولیه هم به عنوان یک حرکت ثبت می‌شود
        if (initialQuantity > 0)
        {
            ApplyChange(
                delta: initialQuantity,
                type: StockMovementType.Initial,
                reason: "موجودی اولیه");
        }
    }

    /// <summary>افزایش موجودی (خرید، مرجوعی مشتری، انتقال ورودی و ...)</summary>
    public void IncreaseQuantity(
        int amount,
        StockMovementType type,
        string? reason = null,
        string? reference = null,
        string? performedBy = null)
    {
        if (amount <= 0)
            throw new ArgumentException("مقدار افزایش باید بیشتر از صفر باشد.", nameof(amount));

        ApplyChange(amount, type, reason, reference, performedBy);
    }

    /// <summary>کاهش موجودی (فروش، مرجوعی به تامین‌کننده، انتقال خروجی و ...)</summary>
    public void DecreaseQuantity(
        int amount,
        StockMovementType type,
        string? reason = null,
        string? reference = null,
        string? performedBy = null)
    {
        if (amount <= 0)
            throw new ArgumentException("مقدار کاهش باید بیشتر از صفر باشد.", nameof(amount));

        ApplyChange(-amount, type, reason, reference, performedBy);
    }

    /// <summary>اصلاح دستی موجودی (انبارگردانی)</summary>
    public void AdjustQuantity(
        int newQuantity,
        string? reason = null,
        string? reference = null,
        string? performedBy = null)
    {
        if (newQuantity < 0)
            throw new ArgumentException("موجودی نمی‌تواند منفی باشد.", nameof(newQuantity));

        var delta = newQuantity - Quantity;

        // اگر موجودی تغییری نکرده، حرکتی هم ثبت نمی‌شود
        if (delta == 0)
            return;

        ApplyChange(
            delta,
            StockMovementType.Adjustment,
            reason ?? "انبارگردانی",
            reference,
            performedBy);
    }

    /// <summary>
    /// نقطهٔ واحد تغییر موجودی: موجودی را تغییر می‌دهد و حرکت متناظر را ثبت می‌کند.
    /// </summary>
    private void ApplyChange(
        int delta,
        StockMovementType type,
        string? reason = null,
        string? reference = null,
        string? performedBy = null)
    {
        if (delta == 0)
            throw new ArgumentException("مقدار تغییر نمی‌تواند صفر باشد.", nameof(delta));

        var newBalance = Quantity + delta;

        if (newBalance < 0)
            throw new InvalidOperationException("موجودی کافی نیست.");

        Quantity = newBalance;

        _movements.Add(new StockMovement(
            inventoryId: Id,
            productId: ProductId,
            warehouseId: WarehouseId,
            quantityChange: delta,
            balanceAfter: Quantity,
            type: type,
            reason: reason,
            reference: reference,
            performedBy: performedBy));

        UpdateModificationTime();
    }
}
