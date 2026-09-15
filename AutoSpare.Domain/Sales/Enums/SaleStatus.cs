namespace AutoSpare.Domain.Sales.Enums;

/// <summary>
/// وضعیت‌های مختلف فاکتور فروش
/// </summary>
public enum SaleStatus
{
    /// <summary>
    /// پیش‌نویس (هنوز نهایی نشده و موجودی کسر نشده است)
    /// </summary>
    Draft = 1,

    /// <summary>
    /// نهایی‌شده (موجودی انبار کسر شده است)
    /// </summary>
    Completed = 2,

    /// <summary>
    /// لغو شده
    /// </summary>
    Cancelled = 3
}

