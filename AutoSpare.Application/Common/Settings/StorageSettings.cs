namespace AutoSpare.Application.Common.Settings;

public class StorageSettings
{
    public const string SectionName = "StorageSettings";

    /// <summary>
    /// مسیر ذخیره‌سازی تصاویر در wwwroot جهت دسترسی در مرورگر
    /// </summary>
    public string ImagesPath { get; set; } = "wwwroot/Uploads/Images";

    /// <summary>
    /// مسیر ذخیره فایل‌های بکاپ دیتابیس
    /// </summary>
    public string BackupPath { get; set; } = "D:/AutoSpareBackups";

    /// <summary>
    /// حداکثر حجم مجاز عکس (برحسب مگابایت)
    /// </summary>
    public int MaxImageSizeInMb { get; set; } = 5;
}