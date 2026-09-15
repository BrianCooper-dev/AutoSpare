using AutoSpare.Domain.Common;

namespace AutoSpare.Domain.Suppliers;

/// <summary>
/// موجودیت تأمین‌کننده کالا و قطعات.
/// </summary>
public class Supplier : BaseEntity
{
    // ==========================================
    // Properties
    // ==========================================

    /// <summary>
    /// نام شرکت یا فروشگاه تأمین‌کننده
    /// </summary>
    public string Name { private set; get; } = string.Empty;

    /// <summary>
    /// نام و نام خانوادگی شخص رابط
    /// </summary>
    public string? ContactPerson { private set; get; }

    /// <summary>
    /// شماره تلفن ثابت
    /// </summary>
    public string? Phone { private set; get; }

    /// <summary>
    /// شماره موبایل (برای اطلاع‌رسانی و پیامک)
    /// </summary>
    public string Mobile { private set; get; } = string.Empty;

    /// <summary>
    /// پست الکترونیکی
    /// </summary>
    public string? Email { private set; get; }

    /// <summary>
    /// آدرس کامل
    /// </summary>
    public string? Address { private set; get; }

    /// <summary>
    /// کد اقتصادی یا شناسه ملی (جهت صدور فاکتور رسمی)
    /// </summary>
    public string? EconomicCode { private set; get; }

    /// <summary>
    /// سقف اعتبار خریدهای نسیه/چکی (تومان/ریال حسب سیستم)
    /// </summary>
    public decimal CreditLimit { private set; get; }

    /// <summary>
    /// وضعیت فعال/غیرفعال بودن تأمین‌کننده در سیستم
    /// </summary>
    public bool IsActive { private set; get; }

    // ==========================================
    // Constructors
    // ==========================================

    /// <summary>
    /// سازنده پارامترلس مخصوص EF Core
    /// </summary>
    private Supplier() { }

    /// <summary>
    /// سازنده اصلی برای ایجاد تأمین‌کننده جدید
    /// </summary>
    public Supplier(
        string name,
        string mobile,
        string? contactPerson = null,
        string? phone = null,
        string? email = null,
        string? address = null,
        string? economicCode = null,
        decimal creditLimit = 0)
    {
        SetName(name);
        SetMobile(mobile);

        ContactPerson = contactPerson?.Trim();
        Phone = phone?.Trim();
        Email = email?.Trim();
        Address = address?.Trim();
        EconomicCode = economicCode?.Trim();

        SetCreditLimit(creditLimit);
        IsActive = true;
    }

    // ==========================================
    // Business Methods / Domain Behaviors
    // ==========================================

    /// <summary>
    /// به‌روزرسانی اطلاعات پایه و تماس تأمین‌کننده
    /// </summary>
    public void UpdateDetails(
        string name,
        string mobile,
        string? contactPerson,
        string? phone,
        string? email,
        string? address,
        string? economicCode)
    {
        SetName(name);
        SetMobile(mobile);

        ContactPerson = contactPerson?.Trim();
        Phone = phone?.Trim();
        Email = email?.Trim();
        Address = address?.Trim();
        EconomicCode = economicCode?.Trim();
    }

    /// <summary>
    /// تغییر سقف اعتبار تأمین‌کننده
    /// </summary>
    public void SetCreditLimit(decimal newLimit)
    {
        if (newLimit < 0)
            throw new ArgumentException("سقف اعتبار نمی‌تواند مقدار منفی باشد.", nameof(newLimit));

        CreditLimit = newLimit;
    }

    /// <summary>
    /// فعال‌سازی تأمین‌کننده
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// غیرفعال‌سازی تأمین‌کننده
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    // ==========================================
    // Internal Helper Validation Methods
    // ==========================================

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("نام تأمین‌کننده نمی‌تواند خالی باشد.", nameof(name));

        Name = name.Trim();
    }

    private void SetMobile(string mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile))
            throw new ArgumentException("شماره موبایل تأمین‌کننده نمی‌تواند خالی باشد.", nameof(mobile));

        Mobile = mobile.Trim();
    }
}
