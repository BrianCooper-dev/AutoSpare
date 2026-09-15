using AutoSpare.Domain.Common;
using AutoSpare.Domain.Inventories.Enums;

namespace AutoSpare.Domain.Inventories;

/// <summary>
/// رکورد تغییرناپذیر تاریخچهٔ موجودی. هر افزایش/کاهش موجودی باید یک StockMovement تولید کند.
/// </summary>
public class StockMovement : BaseEntity
{
    // مالک رکورد
    public Guid InventoryId { get; private set; }
    public Inventory? Inventory { get; private set; }

    // کپی‌شده از Inventory برای گزارش‌گیری سریع (Denormalized)
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }

    /// <summary>مقدار تغییر، علامت‌دار (مثبت = ورود، منفی = خروج)</summary>
    public int QuantityChange { get; private set; }

    /// <summary>موجودی پس از اعمال این حرکت (Snapshot برای Audit)</summary>
    public int BalanceAfter { get; private set; }

    public StockMovementType Type { get; private set; }

    /// <summary>توضیح انسانی (مثلاً «فاکتور فروش شماره ۱۲۳»)</summary>
    public string? Reason { get; private set; }

    /// <summary>شناسهٔ سند مرتبط (شماره فاکتور، رسید انبار و ...)</summary>
    public string? Reference { get; private set; }

    /// <summary>کاربر/عامل ثبت‌کننده</summary>
    public string? PerformedBy { get; private set; }

    /// <summary>زمان وقوع رویداد (متفاوت از CreatedAt که زمان ثبت در دیتابیس است)</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    public bool IsInbound => QuantityChange > 0;

    // سازنده برای EF Core
    protected StockMovement()
    {
    }

    /// <summary>
    /// فقط از داخل Aggregate «Inventory» قابل ساخت است تا هیچ حرکتی بدون تغییر موجودی ثبت نشود.
    /// </summary>
    internal StockMovement(
        Guid inventoryId,
        Guid productId,
        Guid warehouseId,
        int quantityChange,
        int balanceAfter,
        StockMovementType type,
        string? reason = null,
        string? reference = null,
        string? performedBy = null,
        DateTimeOffset? occurredAt = null)
    {
        if (inventoryId == Guid.Empty)
            throw new ArgumentException("شناسه موجودی نامعتبر است.", nameof(inventoryId));

        if (productId == Guid.Empty)
            throw new ArgumentException("شناسه محصول نامعتبر است.", nameof(productId));

        if (warehouseId == Guid.Empty)
            throw new ArgumentException("شناسه انبار نامعتبر است.", nameof(warehouseId));

        if (quantityChange == 0)
            throw new ArgumentException("مقدار تغییر موجودی نمی‌تواند صفر باشد.", nameof(quantityChange));

        if (balanceAfter < 0)
            throw new ArgumentException("موجودی پس از تغییر نمی‌تواند منفی باشد.", nameof(balanceAfter));

        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), "نوع حرکت موجودی معتبر نیست.");

        // اعتبارسنجی جهت حرکت بر اساس نوع آن
        var increases = type.IncreasesStock();
        if (increases.HasValue && increases.Value != (quantityChange > 0))
        {
            throw new ArgumentException(
                $"جهت حرکت برای نوع «{type}» نامعتبر است.", nameof(quantityChange));
        }

        InventoryId = inventoryId;
        ProductId = productId;
        WarehouseId = warehouseId;
        QuantityChange = quantityChange;
        BalanceAfter = balanceAfter;
        Type = type;
        Reason = NormalizeOptional(reason);
        Reference = NormalizeOptional(reference);
        PerformedBy = NormalizeOptional(performedBy);
        OccurredAt = occurredAt ?? DateTimeOffset.UtcNow;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

