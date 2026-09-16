namespace AutoSpare.Domain.DailySettlements.Enums;

/// <summary>
/// وضعیت تسویه روزانه
/// </summary>
public enum SettlementStatus
{
    /// <summary>
    /// باز (در حال ثبت یا ویرایش طی روز)
    /// </summary>
    Open = 1,

    /// <summary>
    /// بسته و نهایی‌شده (قفل برای جلوگیری از تغییر)
    /// </summary>
    Closed = 2
}
