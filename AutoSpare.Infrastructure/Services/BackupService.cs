using System.Globalization;
using AutoSpare.Application.Backup;
using AutoSpare.Application.Backup.DTOs;
using AutoSpare.Application.Common.Settings;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoSpare.Infrastructure.Services;

public class BackupService : IBackupService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly StorageSettings _storageSettings;
    private readonly IOptionsMonitor<BackupSchedulerSettings> _schedulerOptions;
    private readonly ILogger<BackupService> _logger;
    private static readonly PersianCalendar Pc = new();

    public BackupService(
        ApplicationDbContext dbContext,
        IOptions<StorageSettings> storageSettings,
        IOptionsMonitor<BackupSchedulerSettings> schedulerOptions,
        ILogger<BackupService> logger)
    {
        _dbContext = dbContext;
        _storageSettings = storageSettings.Value;
        _schedulerOptions = schedulerOptions;
        _logger = logger;
    }

    public async Task<CreateBackupResultDto> CreateBackupAsync(
        string? customPath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDirectory = string.IsNullOrWhiteSpace(customPath)
                ? _storageSettings.BackupPath
                : customPath.Trim();

            // تضمین مطلق بودن مسیر برای جلوگیری از ذخیره در مسیرهای نامشخص
            targetDirectory = Path.GetFullPath(targetDirectory);

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            var (sourceDbPath, dbName) = GetCurrentDatabaseInfo();
            if (string.IsNullOrWhiteSpace(sourceDbPath) || !File.Exists(sourceDbPath))
            {
                await _dbContext.Database.OpenConnectionAsync(cancellationToken);
                (sourceDbPath, dbName) = GetCurrentDatabaseInfo();

                if (string.IsNullOrWhiteSpace(sourceDbPath) || !File.Exists(sourceDbPath))
                {
                    return new CreateBackupResultDto(false, "امکان تشخیص یا دسترسی به فایل دیتابیس SQLite وجود ندارد.");
                }
            }

            var now = DateTime.Now;
            var persianDateStr =
                $"{Pc.GetYear(now):0000}-{Pc.GetMonth(now):00}-{Pc.GetDayOfMonth(now):00}_{now:HHmmss}";
            var fileName = $"{dbName}_Backup_{persianDateStr}.db";
            var fullPath = Path.Combine(targetDirectory, fileName);

            // اطمینان از مطلق بودن مسیر فایل نهایی
            fullPath = Path.GetFullPath(fullPath);

            _logger.LogInformation("شروع فرآیند بکاپ‌گیری SQLite از {DbName} به مسیر {FullPath}", dbName, fullPath);

            // استفاده از مسیر فایل فیزیکی مبدأ به‌جای رشته اتصال کلی برای پایداری بیشتر در Online Backup API
            var sourceConnectionString = $"Data Source={sourceDbPath};Mode=ReadOnly;";
            var destinationConnectionString = $"Data Source={fullPath}";

            await using (var sourceConn = new SqliteConnection(sourceConnectionString))
            await using (var destinationConn = new SqliteConnection(destinationConnectionString))
            {
                await sourceConn.OpenAsync(cancellationToken);
                await destinationConn.OpenAsync(cancellationToken);

                // اجرای Online Backup API رسمی SQLite
                sourceConn.BackupDatabase(destinationConn);
            }

            // پاک‌سازی پولینگ جهت اطمینان از آزاد شدن قفل فایل و نوشتن کامل روی دیسک
            SqliteConnection.ClearAllPools();

            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                return new CreateBackupResultDto(false,
                    "فایل پشتیبان ایجاد شد اما حجم آن صفر است یا روی دیسک نوشته نشده است.");
            }

            var fileSize = fileInfo.Length;

            _logger.LogInformation("بکاپ با موفقیت ایجاد شد: {FileName} به حجم {Size} بایت", fileName, fileSize);

            CleanupOldBackups(targetDirectory, dbName, fullPath);

            return new CreateBackupResultDto(true, "نسخه پشتیبان SQLite با موفقیت تهیه شد.", fileName, fullPath,
                fileSize);
        }
        catch (SqliteException ex)
        {
            _logger.LogError(ex, "خطای SQLite در زمان تهیه پشتیبان دیتابیس.");
            return new CreateBackupResultDto(false, $"خطای پایگاه داده SQLite: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "عدم دسترسی کافی برای نوشتن فایل در مسیر پشتیبان.");
            return new CreateBackupResultDto(false, "برنامه دسترسی نوشتن (Write) در پوشه تعیین شده را ندارد.");
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
            // پشتیبانی از فایل‌های .db و فایل‌های قدیمی .bak
            var files = dirInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly)
                .Where(f => f.Extension.Equals(".db", StringComparison.OrdinalIgnoreCase) ||
                            f.Extension.Equals(".bak", StringComparison.OrdinalIgnoreCase))
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

    public async Task<(bool IsValid, string Message)> ValidateBackupFileAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
            {
                return (false, "فایل پشتیبان انتخاب‌شده در مسیر مشخص‌شده وجود ندارد.");
            }

            var normalizedPath = Path.GetFullPath(backupFilePath);
            var connStr = new SqliteConnectionStringBuilder
            {
                DataSource = normalizedPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ConnectionString;

            await using var conn = new SqliteConnection(connStr);
            await conn.OpenAsync(cancellationToken);

            // بررسی سلامت ساختار فایل SQLite
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA quick_check;";

            var result = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString();

            if (string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return (true, "فایل پشتیبان معتبر و از نظر ساختار پایگاه داده کاملاً سالم است.");
            }

            return (false, $"فایل بکاپ آسیب دیده یا نامعتبر است: {result}");
        }
        catch (SqliteException ex)
        {
            _logger.LogWarning(ex, "اعتبارسنجی فایل بکاپ ناموفق بود: {Path}", backupFilePath);
            return (false, $"فایل بکاپ نامعتبر است: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطای عمومی در اعتبارسنجی فایل بکاپ.");
            return (false, $"خطا در بررسی فایل: {ex.Message}");
        }
    }

   public async Task<RestoreDatabaseResultDto> RestoreBackupAsync(
    string backupFilePath,
    string? safetyBackupRootDirectory = null,
    CancellationToken cancellationToken = default)
{
    var (sourceDbPath, targetDb) = GetCurrentDatabaseInfo();
    if (string.IsNullOrWhiteSpace(sourceDbPath))
    {
        return new RestoreDatabaseResultDto(false, "امکان تشخیص مسیر پایگاه‌داده جاری جهت بازیابی وجود ندارد.");
    }

    try
    {
        var normalizedBackupPath = Path.GetFullPath(backupFilePath);
        if (!File.Exists(normalizedBackupPath))
        {
            return new RestoreDatabaseResultDto(false, "فایل بکاپ انتخاب‌شده یافت نشد.");
        }

        var (isValid, validationMessage) = await ValidateBackupFileAsync(normalizedBackupPath, cancellationToken);
        if (!isValid)
        {
            return new RestoreDatabaseResultDto(false, $"توقف عملیات: {validationMessage}");
        }

        var rootDir = string.IsNullOrWhiteSpace(safetyBackupRootDirectory)
            ? _storageSettings.BackupPath
            : safetyBackupRootDirectory.Trim();

        rootDir = Path.GetFullPath(rootDir);
        var safetyDir = Path.Combine(rootDir, "Safety");

        _logger.LogWarning("تهیه بکاپ ایمنی قبل از بازیابی دیتابیس {DbName}. مسیر: {SafetyDir}", targetDb, safetyDir);

        var safetyBackupResult = await CreateBackupAsync(safetyDir, cancellationToken);
        if (!safetyBackupResult.IsSuccess)
        {
            _logger.LogError("تهیه بکاپ ایمنی ناموفق بود. عملیات بازیابی متوقف شد. دلیل: {Message}", safetyBackupResult.Message);
            return new RestoreDatabaseResultDto(false, $"عملیات بازیابی متوقف شد: تهیه بکاپ ایمنی ناموفق بود. ({safetyBackupResult.Message})");
        }

        _logger.LogInformation("بکاپ ایمنی با موفقیت ایجاد شد: {SafetyFileName}", safetyBackupResult.FileName);
        _logger.LogWarning("شروع عملیات بازیابی دیتابیس {DbName} از مسیر {FilePath}", targetDb, normalizedBackupPath);

        // استفاده از Online Backup API معکوس (مبدأ: فایل بکاپ | مقصد: دیتابیس زنده)
        // این روش مشکل قفل شدن فایلی File.Copy در ویندوز را ندارد
        var sourceBackupConnStr = $"Data Source={normalizedBackupPath};Mode=ReadOnly;";
        var destinationDbConnStr = _dbContext.Database.GetConnectionString();

        await using (var backupConn = new SqliteConnection(sourceBackupConnStr))
        await using (var currentConn = new SqliteConnection(destinationDbConnStr))
        {
            await backupConn.OpenAsync(cancellationToken);
            await currentConn.OpenAsync(cancellationToken);

            // کپی کامل ساختار و داده‌ها روی دیتابیس فعال
            backupConn.BackupDatabase(currentConn);

            // چک‌پوینت اجباری WAL برای ذخیره قطعی تغییرات در فایل اصلی دیسک
            await using var walCheckpointCmd = currentConn.CreateCommand();
            walCheckpointCmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await walCheckpointCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // پاک‌سازی پولینگ تا تغییرات برای تمام کانتکست‌های EF Core جدید ملموس باشد
        SqliteConnection.ClearAllPools();

        _logger.LogInformation("بازیابی دیتابیس {DbName} با موفقیت به پایان رسید.", targetDb);

        return new RestoreDatabaseResultDto(
            true,
            $"پایگاه‌داده با موفقیت بازیابی شد. (بکاپ ایمنی قبلی: {safetyBackupResult.FileName})",
            targetDb,
            normalizedBackupPath);
    }
    catch (SqliteException ex)
    {
        _logger.LogError(ex, "خطای SQLite حین بازیابی پایگاه‌داده {DbName}", targetDb);
        return new RestoreDatabaseResultDto(false, $"خطای پایگاه داده SQLite: {ex.Message}");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "خطای ناشناخته حین بازیابی پایگاه‌داده {DbName}", targetDb);
        return new RestoreDatabaseResultDto(false, $"خطای سیستمی: {ex.Message}");
    }
}



    public async Task<RestoreDatabaseResultDto> RestoreToTestDatabaseAsync(
        string backupFilePath,
        string targetDatabaseName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetDatabaseName))
        {
            return new RestoreDatabaseResultDto(false, "نام پایگاه‌داده آزمایشی نامعتبر است.");
        }

        try
        {
            var normalizedPath = Path.GetFullPath(backupFilePath);
            if (!File.Exists(normalizedPath))
            {
                return new RestoreDatabaseResultDto(false, "فایل بکاپ انتخاب‌شده یافت نشد.");
            }

            var (isValid, validationMessage) = await ValidateBackupFileAsync(normalizedPath, cancellationToken);
            if (!isValid)
            {
                return new RestoreDatabaseResultDto(false, $"فایل بکاپ معتبر نیست: {validationMessage}");
            }

            var testDbFolder = Path.Combine(_storageSettings.BackupPath, "TestRestoreDb");
            if (!Directory.Exists(testDbFolder))
            {
                Directory.CreateDirectory(testDbFolder);
            }

            var cleanTargetName = Path.GetFileNameWithoutExtension(targetDatabaseName);
            var destDbPath = Path.Combine(testDbFolder, $"{cleanTargetName}.db");

            // اگر فایل قبلی بود، پاک می‌کنیم
            if (File.Exists(destDbPath))
            {
                SqliteConnection.ClearAllPools();
                File.Delete(destDbPath);
            }

            // کپی آنلاین فایل بکاپ به مقصد آزمایشی
            await using (var sourceConn = new SqliteConnection($"Data Source={normalizedPath};Mode=ReadOnly;"))
            await using (var testConn = new SqliteConnection($"Data Source={destDbPath};"))
            {
                await sourceConn.OpenAsync(cancellationToken);
                await testConn.OpenAsync(cancellationToken);
                sourceConn.BackupDatabase(testConn);
            }

            _logger.LogInformation("بازیابی آزمایشی روی دیتابیس {TestDb} با موفقیت انجام شد.", destDbPath);

            return new RestoreDatabaseResultDto(true, "بازیابی آزمایشی با موفقیت انجام شد.", cleanTargetName,
                destDbPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در حین بازیابی آزمایشی روی {TestDb}", targetDatabaseName);
            return new RestoreDatabaseResultDto(false, $"خطا در بازیابی آزمایشی: {ex.Message}");
        }
    }

    private (string FullFilePath, string DatabaseName) GetCurrentDatabaseInfo()
    {
        var connStr = _dbContext.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connStr))
        {
            return (string.Empty, "autospare");
        }

        var builder = new SqliteConnectionStringBuilder(connStr);
        var dataSource = builder.DataSource;

        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return (string.Empty, "autospare");
        }

        var fullPath = Path.GetFullPath(dataSource);
        var dbName = Path.GetFileNameWithoutExtension(fullPath);

        return (fullPath, string.IsNullOrWhiteSpace(dbName) ? "autospare" : dbName);
    }

    private void CleanupOldBackups(string targetDirectory, string dbName, string currentBackupPath)
    {
        try
        {
            var maxBackupCount = _schedulerOptions.CurrentValue.MaxBackupCount;
            if (maxBackupCount <= 0) return;

            var backupPrefix = $"{dbName}_Backup_";
            var currentBackupFullPath = Path.GetFullPath(currentBackupPath);

            var backupFiles = new DirectoryInfo(targetDirectory)
                .GetFiles("*.*", SearchOption.TopDirectoryOnly)
                .Where(file => (file.Extension.Equals(".db", StringComparison.OrdinalIgnoreCase) ||
                                file.Extension.Equals(".bak", StringComparison.OrdinalIgnoreCase)) &&
                               file.Name.StartsWith(backupPrefix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(file => string.Equals(Path.GetFullPath(file.FullName), currentBackupFullPath,
                    StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(file => file.LastWriteTimeUtc)
                .ThenByDescending(file => file.CreationTimeUtc)
                .ToList();

            if (backupFiles.Count <= maxBackupCount) return;

            var filesToDelete = backupFiles.Skip(maxBackupCount).ToList();
            foreach (var file in filesToDelete)
            {
                try
                {
                    file.Delete();
                }
                catch
                {
                    // ignored
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "خطا در فرآیند پاک‌سازی فایل‌های قدیمی بکاپ دیتابیس.");
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
