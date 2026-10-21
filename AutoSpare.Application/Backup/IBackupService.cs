using AutoSpare.Application.Backup.DTOs;

namespace AutoSpare.Application.Backup;

public interface IBackupService
{
    /// <summary>
    /// ایجاد نسخه پشتیبان کامل از دیتابیس به همراه امکان مشخص کردن مسیر سفارشی (اختیاری)
    /// </summary>
    Task<CreateBackupResultDto> CreateBackupAsync(string? customPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// دریافت فهرست فایل‌های بکاپ موجود در پوشه پشتیبان‌گیری
    /// </summary>
    Task<IReadOnlyList<BackupFileDto>> GetBackupFilesAsync(CancellationToken cancellationToken = default);
}

