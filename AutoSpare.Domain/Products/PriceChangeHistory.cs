using AutoSpare.Domain.Common;
using AutoSpare.Domain.Products.Enums;

namespace AutoSpare.Domain.Products;

/// <summary>
/// رکورد تاریخچه تغییرات قیمت خرید و فروش کالا (Immutable Log)
/// </summary>
public class PriceChangeHistory : BaseEntity
{
    /// <summary>
    /// شناسه کالای مرتبط
    /// </summary>
    public Guid ProductId { private set; get; }
    public Product? Product { private set; get; }

    /// <summary>
    /// قیمت خرید قبلی
    /// </summary>
    public decimal OldPurchasePrice { private set; get; }

    /// <summary>
    /// قیمت خرید جدید
    /// </summary>
    public decimal NewPurchasePrice { private set; get; }

    /// <summary>
    /// قیمت فروش قبلی
    /// </summary>
    public decimal OldSalePrice { private set; get; }

    /// <summary>
    /// قیمت فروش جدید
    /// </summary>
    public decimal NewSalePrice { private set; get; }

    /// <summary>
    /// نوع تغییر قیمت (دستی، درصدی، گروهی و ...)
    /// </summary>
    public PriceChangeType ChangeType { private set; get; }

    /// <summary>
    /// تاریخ و ساعت اعمال تغییر قیمت
    /// </summary>
    public DateTime ChangeDate { private set; get; }

    /// <summary>
    /// دلیل یا یادداشت تغییر قیمت
    /// </summary>
    public string? Reason { private set; get; }

    // ==========================================
    // Calculated Properties (ویژگی‌های محاسباتی)
    // ==========================================

    /// <summary>
    /// اختلاف قیمت فروش (جدید منهای قدیم)
    /// </summary>
    public decimal SalePriceDifference => NewSalePrice - OldSalePrice;

    /// <summary>
    /// درصد تغییر قیمت فروش
    /// </summary>
    public decimal SalePricePercentageChange =>
        OldSalePrice > 0 ? Math.Round(((NewSalePrice - OldSalePrice) / OldSalePrice) * 100, 2) : 0;

    // سازنده مخصوص EF Core
    private PriceChangeHistory() { }

    public PriceChangeHistory(
        Guid productId,
        decimal oldPurchasePrice,
        decimal newPurchasePrice,
        decimal oldSalePrice,
        decimal newSalePrice,
        PriceChangeType changeType,
        string? reason = null,
        DateTime? changeDate = null)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("شناسه کالا نامعتبر است.", nameof(productId));

        ValidatePrice(oldPurchasePrice, nameof(oldPurchasePrice));
        ValidatePrice(newPurchasePrice, nameof(newPurchasePrice));
        ValidatePrice(oldSalePrice, nameof(oldSalePrice));
        ValidatePrice(newSalePrice, nameof(newSalePrice));

        ProductId = productId;
        OldPurchasePrice = oldPurchasePrice;
        NewPurchasePrice = newPurchasePrice;
        OldSalePrice = oldSalePrice;
        NewSalePrice = newSalePrice;
        ChangeType = changeType;
        Reason = reason?.Trim();
        ChangeDate = changeDate ?? DateTime.UtcNow;
    }

    private static void ValidatePrice(decimal price, string paramName)
    {
        if (price < 0)
            throw new ArgumentException("مبلغ قیمت نمی‌تواند منفی باشد.", paramName);
    }
}

