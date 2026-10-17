using System.Globalization;

namespace AutoSpare.Application.DailySettlements.DTOs;

public class DailySettlementDto
{
    private static readonly PersianCalendar _pc = new();

    public Guid Id { get; set; }
    public DateTime SettlementDate { get; set; }
    public decimal Pos1Amount { get; set; }
    public decimal Pos2Amount { get; set; }
    public decimal CashAmount { get; set; }
    public decimal TotalAmount => Pos1Amount + Pos2Amount + CashAmount;
    public int Status { get; set; }
    public string StatusTitle => Status == 1 ? "باز" : "بسته شده";
    public string? Notes { get; set; }

    /// <summary>
    /// عنوان روز هفته محاسبه‌شده از روی تاریخ (بدون ذخیره در دیتابیس)
    /// </summary>
    public string DayOfWeekTitle => SettlementDate.DayOfWeek switch
    {
        DayOfWeek.Saturday => "شنبه",
        DayOfWeek.Sunday => "یک‌شنبه",
        DayOfWeek.Monday => "دوشنبه",
        DayOfWeek.Tuesday => "سه‌شنبه",
        DayOfWeek.Wednesday => "چهارشنبه",
        DayOfWeek.Thursday => "پنج‌شنبه",
        DayOfWeek.Friday => "جمعه",
        _ => string.Empty
    };

    /// <summary>
    /// تاریخ شمسی فرمت‌شده
    /// </summary>
    public string PersianDateString =>
        $"{_pc.GetYear(SettlementDate)}/{_pc.GetMonth(SettlementDate):00}/{_pc.GetDayOfMonth(SettlementDate):00}";
}
