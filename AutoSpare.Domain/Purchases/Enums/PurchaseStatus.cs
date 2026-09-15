namespace AutoSpare.Domain.Purchases.Enums;

/// <summary>
/// وضعیت‌های مختلف فاکتور خرید
/// </summary>
public enum PurchaseStatus
{
    /// <summary>
    /// پیش‌نویس (هنوز نهایی نشده و در موجودی انبار اعمال نشده)
    /// </summary>
    Draft = 1,

    /// <summary>
    /// نهایی‌شده (موجودی انبار افزایش یافته است)
    /// </summary>
    Completed = 2,

    /// <summary>
    /// لغو شده
    /// </summary>
    Cancelled = 3
}
