namespace AutoSpare.Domain.Products.Enums;

/// <summary>
/// نوع عملیات تغییر قیمت
/// </summary>
public enum PriceChangeType
{
    /// <summary>
    /// تغییر دستی قیمت یک کالا
    /// </summary>
    Manual = 1,

    /// <summary>
    /// تغییر گروهی درصدی (مثلاً افزایش ۱۰٪ به همه کالاهای یک برند)
    /// </summary>
    BatchPercentage = 2,

    /// <summary>
    /// تغییر گروهی مبلغ ثابت (مثلاً اضافه شدن ۵۰ هزار تومان به همه کالاها)
    /// </summary>
    BatchFixedAmount = 3,

    /// <summary>
    /// به‌روزرسانی ناشی از ثبت فاکتور خرید جدید
    /// </summary>
    PurchaseUpdate = 4,

    /// <summary>
    /// واردات از اکسل یا همگام‌سازی از لیست قیمت تامین‌کننده
    /// </summary>
    Import = 5
}

