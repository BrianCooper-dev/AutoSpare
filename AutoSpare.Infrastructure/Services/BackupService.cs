using System.Globalization;
using AutoSpare.Application.Backup;
using AutoSpare.Application.Backup.DTOs;
using AutoSpare.Application.Common.Settings;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoSpare.Infrastructure.Services;

public class BackupService : IBackupService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly StorageSettings _storageSettings;
    private readonly ILogger<BackupService> _logger;
    private static readonly PersianCalendar Pc = new();

    public BackupService(
        ApplicationDbContext dbContext,
        IOptions<StorageSettings> storageSettings,
        ILogger<BackupService> logger)
    {
        _dbContext = dbContext;
        _storageSettings = storageSettings.Value;
        _logger = logger;
    }

    public async Task<CreateBackupResultDto> CreateBackupAsync(string? customPath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDirectory = string.IsNullOrWhiteSpace(customPath)
                ? _storageSettings.BackupPath
                : customPath.Trim();

            // نرمال‌سازی مسیر برای ویندوز
            targetDirectory = Path.GetFullPath(targetDirectory);

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // استخراج نام دیتابیس از کانکشن جاری
            var connection = _dbContext.Database.GetDbConnection();
            var dbName = connection.Database;

            if (string.IsNullOrWhiteSpace(dbName))
            {
                var builder = new SqlConnectionStringBuilder(connection.ConnectionString);
                dbName = builder.InitialCatalog;
            }

            if (string.IsNullOrWhiteSpace(dbName))
            {
                return new CreateBackupResultDto(false, "امکان تشخیص نام پایگاه داده از رشته اتصال وجود ندارد.");
            }

            // تولید نام فایل یکتا: AutoSpareDb_Backup_1403-08-01_143025.bak
            var now = DateTime.Now;
            var persianDateStr =
                $"{Pc.GetYear(now):0000}-{Pc.GetMonth(now):00}-{Pc.GetDayOfMonth(now):00}_{now:HHmmss}";
            var fileName = $"{dbName}_Backup_{persianDateStr}.bak";
            var fullPath = Path.Combine(targetDirectory, fileName);

            // اجرای دستور T-SQL برای بکاپ
            // به جهت جلوگیری از SQL Injection نام دیتابیس داخل براکت قرار داده می‌شود و مسیر به صورت پارامتریک یا ایمن فرمت می‌شود
            var safeDbName = dbName.Replace("]", "]]");
            var sql =
                $"BACKUP DATABASE [{safeDbName}] TO DISK = @backupPath WITH FORMAT, INIT, NAME = @backupName, SKIP, NOREWIND, NOUNLOAD, STATS = 10";

            var backupPathParam = new SqlParameter("@backupPath", fullPath);
            var backupNameParam = new SqlParameter("@backupName", $"AutoSpare Full Backup - {persianDateStr}");

            _logger.LogInformation("شروع فرآیند بکاپ‌گیری از دیتابیس {DbName} در مسیر {FullPath}", dbName, fullPath);

            await _dbContext.Database.ExecuteSqlRawAsync(sql, [backupPathParam, backupNameParam], cancellationToken);

            var fileInfo = new FileInfo(fullPath);
            var fileSize = fileInfo.Exists ? fileInfo.Length : 0;

            _logger.LogInformation("بکاپ با موفقیت ایجاد شد: {FileName} به حجم {Size} بایت", fileName, fileSize);

            return new CreateBackupResultDto(true, "نسخه پشتیبان با موفقیت تهیه شد.", fileName, fullPath, fileSize);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "خطای SQL Server در زمان تهیه پشتیبان دیتابیس.");
            return new CreateBackupResultDto(false, $"خطای پایگاه داده: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "عدم دسترسی کافی برای نوشتن فایل در مسیر پشتیبان.");
            return new CreateBackupResultDto(false,
                "سرویس SQL Server یا برنامه دسترسی نوشتن (Write) در پوشه تعیین شده را ندارد.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطای ناشناخته در بکاپ‌گیری.");
            return new CreateBackupResultDto(false, $"خطای سیستمی: {ex.Message}");
        }
    }

    public Task<IReadOnlyList<BackupFileDto>> GetBackupFilesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDirectory = Path.GetFullPath(_storageSettings.BackupPath);

            if (!Directory.Exists(targetDirectory))
            {
                return Task.FromResult<IReadOnlyList<BackupFileDto>>(Array.Empty<BackupFileDto>());
            }

            var dirInfo = new DirectoryInfo(targetDirectory);
            var files = dirInfo.GetFiles("*.bak", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => f.CreationTimeUtc)
                .Select(f =>
                {
                    var localTime = f.CreationTime;
                    var persianStr =
                        $"{Pc.GetYear(localTime):0000}/{Pc.GetMonth(localTime):00}/{Pc.GetDayOfMonth(localTime):00} - {localTime:HH:mm:ss}";
                    return new BackupFileDto
                    {
                        FileName = f.Name,
                        FullPath = f.FullName,
                        SizeInBytes = f.Length,
                        FileSizeFormatted = FormatBytes(f.Length),
                        CreatedAtUtc = f.CreationTimeUtc,
                        PersianCreatedAt = persianStr
                    };
                })
                .ToList();

            return Task.FromResult<IReadOnlyList<BackupFileDto>>(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در دریافت لیست فایل‌های بکاپ.");
            return Task.FromResult<IReadOnlyList<BackupFileDto>>(Array.Empty<BackupFileDto>());
        }
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 * 1024 => $"{(double)bytes / (1024 * 1024 * 1024):0.##} GB",
            >= 1024 * 1024 => $"{(double)bytes / (1024 * 1024):0.##} MB",
            >= 1024 => $"{(double)bytes / 1024:0.##} KB",
            _ => $"{bytes} B"
        };
    }
}
