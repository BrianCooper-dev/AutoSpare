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

            targetDirectory = Path.GetFullPath(targetDirectory);

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            var dbName = GetCurrentDatabaseName();
            if (string.IsNullOrWhiteSpace(dbName))
            {
                return new CreateBackupResultDto(false, "امکان تشخیص نام پایگاه داده از رشته اتصال وجود ندارد.");
            }

            var now = DateTime.Now;
            var persianDateStr =
                $"{Pc.GetYear(now):0000}-{Pc.GetMonth(now):00}-{Pc.GetDayOfMonth(now):00}_{now:HHmmss}";
            var fileName = $"{dbName}_Backup_{persianDateStr}.bak";
            var fullPath = Path.Combine(targetDirectory, fileName);

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

            CleanupOldBackups(targetDirectory, dbName, fullPath);

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

            var masterConnString = GetMasterConnectionString();
            await using var conn = new SqlConnection(masterConnString);
            await conn.OpenAsync(cancellationToken);

            var sql = "RESTORE VERIFYONLY FROM DISK = @backupPath";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.CommandTimeout = 180;
            cmd.Parameters.AddWithValue("@backupPath", Path.GetFullPath(backupFilePath));

            await cmd.ExecuteNonQueryAsync(cancellationToken);

            return (true, "فایل پشتیبان معتبر و از نظر ساختار پایگاه داده سالم است.");
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "اعتبارسنجی فایل بکاپ ناموفق بود: {Path}", backupFilePath);
            return (false, $"فایل بکاپ نامعتبر یا آسیب‌دیده است: {ex.Message}");
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
        var targetDb = GetCurrentDatabaseName();
        if (string.IsNullOrWhiteSpace(targetDb))
        {
            return new RestoreDatabaseResultDto(false, "امکان تشخیص نام پایگاه داده جاری جهت بازیابی وجود ندارد.");
        }

        try
        {
            var normalizedPath = Path.GetFullPath(backupFilePath);
            if (!File.Exists(normalizedPath))
            {
                return new RestoreDatabaseResultDto(false, "فایل بکاپ انتخاب‌شده یافت نشد.");
            }

            // ۱) اعتبارسنجی اولیه فایل پیش از انجام هر تغییری در وضعیت دیتابیس
            var (isValid, validationMessage) = await ValidateBackupFileAsync(normalizedPath, cancellationToken);
            if (!isValid)
            {
                return new RestoreDatabaseResultDto(false, $"توقف عملیات: {validationMessage}");
            }

            // ۲) تهیه بکاپ ایمنی از وضعیت فعلی قبل از Restore (داخل Safety تا توسط retention حذف نشود)
            var rootDir = string.IsNullOrWhiteSpace(safetyBackupRootDirectory)
                ? _storageSettings.BackupPath
                : safetyBackupRootDirectory.Trim();

            rootDir = Path.GetFullPath(rootDir);

            var safetyDir = Path.Combine(rootDir, "Safety");

            _logger.LogWarning(
                "تهیه بکاپ ایمنی قبل از بازیابی دیتابیس {DbName}. مسیر: {SafetyDir}",
                targetDb, safetyDir);

            var safetyBackupResult = await CreateBackupAsync(safetyDir, cancellationToken);
            if (!safetyBackupResult.IsSuccess)
            {
                _logger.LogError(
                    "تهیه بکاپ ایمنی ناموفق بود. عملیات بازیابی متوقف شد. دلیل: {Message}",
                    safetyBackupResult.Message);

                return new RestoreDatabaseResultDto(false,
                    $"عملیات بازیابی متوقف شد: تهیه بکاپ ایمنی از وضعیت فعلی ناموفق بود. ({safetyBackupResult.Message})");
            }

            _logger.LogInformation(
                "بکاپ ایمنی با موفقیت ایجاد شد: {SafetyFileName} | {SafetyFullPath}",
                safetyBackupResult.FileName, safetyBackupResult.FullPath);

            _logger.LogWarning("شروع عملیات بازیابی دیتابیس {DbName} از مسیر {FilePath}", targetDb, normalizedPath);

            // ۳) باز کردن اتصال مستقیم به دیتابیس master جهت جلوگیری از قفل شدن فرآیند ریستور روی دیتابیس جاری
            var masterConnString = GetMasterConnectionString();
            await using var connection = new SqlConnection(masterConnString);
            await connection.OpenAsync(cancellationToken);

            var safeDbName = targetDb.Replace("]", "]]");

            // ۴) قطع کانکشن‌ها، بازگردانی کامل و بازگردانی وضعیت به MULTI_USER
            var restoreScript = $@"
                ALTER DATABASE [{safeDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                RESTORE DATABASE [{safeDbName}] FROM DISK = @backupPath WITH REPLACE;
                ALTER DATABASE [{safeDbName}] SET MULTI_USER;
            ";

            await using var command = new SqlCommand(restoreScript, connection)
            {
                CommandTimeout = 300
            };
            command.Parameters.AddWithValue("@backupPath", normalizedPath);

            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation(
                "بازیابی پایگاه‌داده {DbName} با موفقیت به پایان رسید. بکاپ ایمنی: {SafetyFileName}",
                targetDb, safetyBackupResult.FileName);

            return new RestoreDatabaseResultDto(
                true,
                $"پایگاه‌داده با موفقیت بازیابی شد. بکاپ ایمنی وضعیت قبلی: {safetyBackupResult.FileName}",
                targetDb,
                normalizedPath);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "خطای SQL Server حین بازیابی پایگاه‌داده {DbName}", targetDb);
            TryResetDatabaseToMultiUser(targetDb);
            return new RestoreDatabaseResultDto(false, $"خطا در بازیابی پایگاه‌داده: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطای عمومی در فرآیند بازیابی پایگاه‌داده {DbName}", targetDb);
            TryResetDatabaseToMultiUser(targetDb);
            return new RestoreDatabaseResultDto(false, $"خطای سیستمی: {ex.Message}");
        }
    }

    private string GetCurrentDatabaseName()
    {
        var connection = _dbContext.Database.GetDbConnection();
        var dbName = connection.Database;

        if (string.IsNullOrWhiteSpace(dbName))
        {
            var builder = new SqlConnectionStringBuilder(connection.ConnectionString);
            dbName = builder.InitialCatalog;
        }

        return dbName;
    }

    private string GetMasterConnectionString()
    {
        var connection = _dbContext.Database.GetDbConnection();
        var builder = new SqlConnectionStringBuilder(connection.ConnectionString)
        {
            InitialCatalog = "master",
            Pooling = false // جلوگیری از نگه‌داشتن نشست‌های معلق در Pool
        };
        return builder.ConnectionString;
    }

    private void TryResetDatabaseToMultiUser(string dbName)
    {
        try
        {
            var masterConnString = GetMasterConnectionString();
            using var connection = new SqlConnection(masterConnString);
            connection.Open();
            var safeDbName = dbName.Replace("]", "]]");
            using var cmd = new SqlCommand($"ALTER DATABASE [{safeDbName}] SET MULTI_USER WITH ROLLBACK IMMEDIATE;",
                connection);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "تلاش برای بازگردانی دیتابیس به وضعیت MULTI_USER با خطا مواجه شد.");
        }
    }

    private void CleanupOldBackups(string targetDirectory, string dbName, string currentBackupPath)
    {
        try
        {
            var maxBackupCount = _schedulerOptions.CurrentValue.MaxBackupCount;

            if (maxBackupCount <= 0)
            {
                _logger.LogInformation("سیاست پاک‌سازی خودکار بکاپ‌ها غیرفعال است (MaxBackupCount: {MaxBackupCount}).",
                    maxBackupCount);
                return;
            }

            var backupPrefix = $"{dbName}_Backup_";
            var currentBackupFullPath = Path.GetFullPath(currentBackupPath);

            var backupFiles = new DirectoryInfo(targetDirectory)
                .GetFiles("*.bak", SearchOption.TopDirectoryOnly)
                .Where(file => file.Name.StartsWith(backupPrefix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(file => string.Equals(Path.GetFullPath(file.FullName), currentBackupFullPath,
                    StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(file => file.LastWriteTimeUtc)
                .ThenByDescending(file => file.CreationTimeUtc)
                .ToList();

            if (backupFiles.Count <= maxBackupCount)
            {
                _logger.LogInformation(
                    "تعداد بکاپ‌های موجود ({CurrentCount}) از سقف مجاز ({MaxCount}) بیشتر نیست. فایلی حذف نشد.",
                    backupFiles.Count, maxBackupCount);
                return;
            }

            var filesToDelete = backupFiles
                .Skip(maxBackupCount)
                .ToList();

            foreach (var file in filesToDelete)
            {
                try
                {
                    file.Delete();
                    _logger.LogInformation("بکاپ مازاد قدیمی با موفقیت حذف گردید: {FileName}", file.Name);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning(ex, "دسترسی مجاز برای حذف فایل بکاپ {FileName} وجود ندارد.", file.FullName);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "خطای ورودی/خروجی حین حذف فایل بکاپ {FileName}.", file.FullName);
                }
            }

            _logger.LogInformation(
                "فرآیند پاک‌سازی بکاپ‌ها خاتمه یافت. تعداد حذف‌شده: {DeletedCount} | تعداد مجاز: {MaxCount}",
                filesToDelete.Count, maxBackupCount);
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

            var masterConnString = GetMasterConnectionString();
            await using var connection = new SqlConnection(masterConnString);
            await connection.OpenAsync(cancellationToken);

            // ۱) استخراج نام منطقی فایل‌های دیتابیس (Logical Names) از درون بکاپ
            var fileListQuery = "RESTORE FILELISTONLY FROM DISK = @backupPath";
            string? logicalDataName = null;
            string? logicalLogName = null;

            await using (var fileCmd = new SqlCommand(fileListQuery, connection))
            {
                fileCmd.Parameters.AddWithValue("@backupPath", normalizedPath);
                await using var reader = await fileCmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var logicalName = reader["LogicalName"].ToString();
                    var type = reader["Type"].ToString(); // D = Data, L = Log
                    if (type == "D" && logicalDataName is null)
                        logicalDataName = logicalName;
                    else if (type == "L" && logicalLogName is null)
                        logicalLogName = logicalName;
                }
            }

            if (string.IsNullOrWhiteSpace(logicalDataName) || string.IsNullOrWhiteSpace(logicalLogName))
            {
                return new RestoreDatabaseResultDto(false, "امکان خواندن ساختار فایل‌های منطقی درون بکاپ وجود ندارد.");
            }

            // ۲) تعیین مسیر فیزیکی مجزا برای دیتابیس آزمایشی در پوشه بکاپ تا با فایل‌های دیتابیس اصلی تداخل نداشته باشد
            var testDbFolder = Path.Combine(_storageSettings.BackupPath, "TestRestoreDb");
            if (!Directory.Exists(testDbFolder))
            {
                Directory.CreateDirectory(testDbFolder);
            }

            var destMdf = Path.Combine(testDbFolder, $"{targetDatabaseName}.mdf");
            var destLdf = Path.Combine(testDbFolder, $"{targetDatabaseName}_log.ldf");

            var safeTargetDb = targetDatabaseName.Replace("]", "]]");

            // ۳) اجرای RESTORE با MOVE به مسیرهای جدید
            var restoreSql = $@"
                IF EXISTS (SELECT name FROM sys.databases WHERE name = @targetDb)
                BEGIN
                    ALTER DATABASE [{safeTargetDb}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{safeTargetDb}];
                END

                RESTORE DATABASE [{safeTargetDb}]
                FROM DISK = @backupPath
                WITH MOVE @logicalData TO @destMdf,
                     MOVE @logicalLog TO @destLdf,
                     REPLACE, STATS = 10;

                ALTER DATABASE [{safeTargetDb}] SET MULTI_USER;
            ";

            await using var restoreCmd = new SqlCommand(restoreSql, connection)
            {
                CommandTimeout = 300
            };
            restoreCmd.Parameters.AddWithValue("@targetDb", targetDatabaseName);
            restoreCmd.Parameters.AddWithValue("@backupPath", normalizedPath);
            restoreCmd.Parameters.AddWithValue("@logicalData", logicalDataName);
            restoreCmd.Parameters.AddWithValue("@destMdf", destMdf);
            restoreCmd.Parameters.AddWithValue("@logicalLog", logicalLogName);
            restoreCmd.Parameters.AddWithValue("@destLdf", destLdf);

            _logger.LogInformation("شروع بازیابی آزمایشی بر روی دیتابیس {TestDb}...", targetDatabaseName);
            await restoreCmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("بازیابی آزمایشی روی دیتابیس {TestDb} با موفقیت انجام شد.", targetDatabaseName);

            return new RestoreDatabaseResultDto(true, "بازیابی آزمایشی با موفقیت انجام شد.", targetDatabaseName,
                normalizedPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در حین بازیابی آزمایشی روی {TestDb}", targetDatabaseName);
            return new RestoreDatabaseResultDto(false, $"خطا در بازیابی آزمایشی: {ex.Message}");
        }
    }
}
