using System.Globalization;

namespace AutoSpare.Application.Common.RightAligned;

public static class PersianExtensions
{
    private static readonly PersianCalendar pc = new PersianCalendar();

    // تبدیل میلادی به تاریخ شمسی ساده: ۱۴۰۳/۰۷/۰۵
    public static string ToPersianDate(this DateTime date)
    {
        return $"{pc.GetYear(date):0000}/{pc.GetMonth(date):02}/{pc.GetDayOfMonth(date):02}";
    }

    // تاریخ با ساعت: ۱۴۰۳/۰۷/۰۵ ۱4:۳۰
    public static string ToPersianDateTime(this DateTime date)
    {
        return $"{pc.GetYear(date):0000}/{pc.GetMonth(date):02}/{pc.GetDayOfMonth(date):02} {date:HH:mm}";
    }

    // فرمت سه‌رقم سه‌رقم مبالغ (قیمت/تومان)
    public static string ToPersianPrice(this decimal amount, bool includeUnit = true)
    {
        var formatted = amount.ToString("N0", CultureInfo.InvariantCulture);
        return includeUnit ? $"{formatted} تومان" : formatted;
    }
}
