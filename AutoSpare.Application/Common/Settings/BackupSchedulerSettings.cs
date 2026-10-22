namespace AutoSpare.Application.Common.Settings;

public sealed class BackupSchedulerSettings
{
    public const string SectionName = "BackupScheduler";

    /// <summary>
    /// فعال یا غیرفعال بودن بکاپ‌گیری خودکار
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// ساعت‌های اجرای بکاپ با فرمت HH:mm مانند ["02:00", "22:30"]
    /// </summary>
    public List<string> Times { get; set; } = new();

    /// <summary>
    /// محاسبه زمان بر پایه ساعت محلی سرور یا UTC
    /// </summary>
    public bool UseUtc { get; set; } = false;

    /// <summary>
    /// مسیر جایگزین برای ذخیره بکاپ
    /// </summary>
    public string? BackupPathOverride { get; set; }

    /// <summary>
    /// اجرای یک بکاپ بلافاصله پس از بالا آمدن برنامه
    /// </summary>
    public bool RunOnStartup { get; set; } = false;

    /// <summary>
    /// حداکثر تعداد فایل‌های بکاپ قابل نگهداری برای هر دیتابیس.
    /// مقدار صفر یعنی پاک‌سازی خودکار غیرفعال است.
    /// </summary>
    public int MaxBackupCount { get; set; } = 30;
}
