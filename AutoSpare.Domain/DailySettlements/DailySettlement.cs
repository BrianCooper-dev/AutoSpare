using AutoSpare.Domain.Common;
using AutoSpare.Domain.DailySettlements.Enums;

namespace AutoSpare.Domain.DailySettlements;

/// <summary>
/// تسویه روزانه صندوق و دستگاه‌های کارت‌خوان
/// </summary>
public class DailySettlement : BaseEntity
{
    /// <summary>
    /// تاریخ تسویه (فقط تاریخ روز مدنظر است)
    /// </summary>
    public DateTime SettlementDate { private set; get; }

    /// <summary>
    /// مبلغ دریافتی کارت‌خوان شماره ۱
    /// </summary>
    public decimal Pos1Amount { private set; get; }

    /// <summary>
    /// مبلغ دریافتی کارت‌خوان شماره ۲
    /// </summary>
    public decimal Pos2Amount { private set; get; }

    /// <summary>
    /// مبلغ دریافتی نقد
    /// </summary>
    public decimal CashAmount { private set; get; }

    /// <summary>
    /// جمع کل تسویه روزانه
    /// </summary>
    public decimal TotalAmount => Pos1Amount + Pos2Amount + CashAmount;

    /// <summary>
    /// وضعیت تسویه (باز یا بسته)
    /// </summary>
    public SettlementStatus Status { private set; get; }

    /// <summary>
    /// یادداشت‌ها، توضیحات یا مغایرت‌های احتمالی
    /// </summary>
    public string? Notes { private set; get; }

    // سازنده مخصوص EF Core
    private DailySettlement() { }

    public DailySettlement(
        DateTime settlementDate,
        decimal pos1Amount = 0,
        decimal pos2Amount = 0,
        decimal cashAmount = 0,
        string? notes = null)
    {
        ValidateAmount(pos1Amount, nameof(pos1Amount));
        ValidateAmount(pos2Amount, nameof(pos2Amount));
        ValidateAmount(cashAmount, nameof(cashAmount));

        SettlementDate = settlementDate.Date;
        Pos1Amount = pos1Amount;
        Pos2Amount = pos2Amount;
        CashAmount = cashAmount;
        Status = SettlementStatus.Open;
        Notes = notes?.Trim();
    }

    // ==========================================
    // رفتارهای بیزینسی (Business Methods)
    // ==========================================

    /// <summary>
    /// به‌روزرسانی مبالغ دریافتی (فقط زمانی که تسویه هنوز باز است)
    /// </summary>
    public void UpdateAmounts(decimal pos1Amount, decimal pos2Amount, decimal cashAmount, string? notes = null)
    {
        EnsureIsOpen();

        ValidateAmount(pos1Amount, nameof(pos1Amount));
        ValidateAmount(pos2Amount, nameof(pos2Amount));
        ValidateAmount(cashAmount, nameof(cashAmount));

        Pos1Amount = pos1Amount;
        Pos2Amount = pos2Amount;
        CashAmount = cashAmount;
        Notes = notes?.Trim();
    }

    /// <summary>
    /// بستن و نهایی‌سازی تسویه روزانه (قفل دخل)
    /// </summary>
    public void CloseSettlement()
    {
        EnsureIsOpen();
        Status = SettlementStatus.Closed;
    }

    /// <summary>
    /// بازگشایی مجدد تسویه در صورت نیاز به اصلاح توسط مدیر
    /// </summary>
    public void ReopenSettlement()
    {
        Status = SettlementStatus.Open;
    }

    // ==========================================
    // اعتبارسنجی‌ها (Helpers)
    // ==========================================

    private void EnsureIsOpen()
    {
        if (Status != SettlementStatus.Open)
            throw new InvalidOperationException("این تسویه روزانه بسته شده است و امکان تغییر مقادیر آن وجود ندارد.");
    }

    private static void ValidateAmount(decimal amount, string paramName)
    {
        if (amount < 0)
            throw new ArgumentException("مبلغ نمی‌تواند منفی باشد.", paramName);
    }
}

