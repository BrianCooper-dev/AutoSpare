using AutoSpare.Application.Backup.DTOs;

namespace AutoSpare.Application.Backup;

public interface IBackupService
{
    /// <summary>
    /// ایجاد نسخه پشتیبان کامل از دیتابیس به همراه امکان مشخص کردن مسیر سفارشی (اختیاری)
    /// </summary>
    Task<CreateBackupResultDto> CreateBackupAsync(
        string? customPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// دریافت فهرست فایل‌های بکاپ موجود در پوشه پشتیبان‌گیری
    /// </summary>
    Task<IReadOnlyList<BackupFileDto>> GetBackupFilesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// اعتبارسنجی و سلامت‌سنجی فایل پشتیبان با استفاده از RESTORE VERIFYONLY
    /// </summary>
    Task<(bool IsValid, string Message)> ValidateBackupFileAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// بازیابی کامل دیتابیس از روی فایل مشخص‌شده با انتقال کانکشن به master و بستن سشن‌های فعال
    /// قبل از Restore یک بکاپ ایمنی (Safety Backup) از وضعیت فعلی گرفته می‌شود.
    /// </summary>
    Task<RestoreDatabaseResultDto> RestoreBackupAsync(
        string backupFilePath,
        string? safetyBackupRootDirectory = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// بازیابی نسخه پشتیبان روی یک دیتابیس مشخص (آزمایشی) جهت تست و اعتبارسنجی داده‌ها
    /// </summary>
    Task<RestoreDatabaseResultDto> RestoreToTestDatabaseAsync(
        string backupFilePath,
        string targetDatabaseName,
        CancellationToken cancellationToken = default);
}
