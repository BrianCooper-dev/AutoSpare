namespace AutoSpare.Application.Common.Settings;

public sealed class StorageSettings
{
    public const string SectionName = "StorageSettings";

    /// <summary>
    /// مسیر تصاویر نسبت به wwwroot، یا یک مسیر مطلق.
    /// </summary>
    public string ImagesPath { get; set; } = "Uploads/Images";

    /// <summary>
    /// مسیر ذخیره فایل‌های بکاپ.
    /// </summary>
    public string BackupPath { get; set; } = "Backups";

    /// <summary>
    /// حداکثر حجم مجاز هر تصویر برحسب مگابایت.
    /// </summary>
    public int MaxImageSizeInMb { get; set; } = 5;
}