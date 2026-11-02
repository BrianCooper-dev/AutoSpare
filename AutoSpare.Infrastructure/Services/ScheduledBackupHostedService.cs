using System.Globalization;
using AutoSpare.Application.Backup;
using AutoSpare.Application.Backup.DTOs;
using AutoSpare.Application.Common.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoSpare.Infrastructure.Services;

public sealed class ScheduledBackupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<BackupSchedulerSettings> _schedulerOptions;
    private readonly IOptionsMonitor<StorageSettings> _storageOptions;
    private readonly ILogger<ScheduledBackupHostedService> _logger;

    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly string[] SupportedTimeFormats = ["hh\\:mm", "h\\:mm", "HH\\:mm", "H\\:mm"];
    private static readonly PersianCalendar Pc = new();

    public ScheduledBackupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<BackupSchedulerSettings> schedulerOptions,
        IOptionsMonitor<StorageSettings> storageOptions,
        ILogger<ScheduledBackupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _schedulerOptions = schedulerOptions;
        _storageOptions = storageOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("سرویس زمان‌بندی بکاپ پایگاه داده راه‌اندازی شد.");

        var initialCfg = _schedulerOptions.CurrentValue;

        if (initialCfg.RunOnStartup && initialCfg.Enabled)
        {
            _ = Task.Run(() => TryRunBackupAsync("Startup", stoppingToken), stoppingToken);
        }

        await CheckAndRunBackupIfMissingAsync("Startup Check", stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var cfg = _schedulerOptions.CurrentValue;

            if (!cfg.Enabled)
            {
                _logger.LogDebug("سرویس زمان‌بندی بکاپ غیرفعال است. تلاش مجدد در ۵ دقیقه دیگر.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                continue;
            }

            await CheckAndRunBackupIfMissingAsync("Scheduled Check", stoppingToken);

            var now = cfg.UseUtc ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
            var next = GetNextRun(now, cfg.Times);

            if (next is null)
            {
                _logger.LogWarning(
                    "هیچ ساعت زمان‌بندی معتبری در BackupScheduler:Times یافت نشد. تلاش مجدد تا ۱۰ دقیقه دیگر.");
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                continue;
            }

            var delay = next.Value - now;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            _logger.LogInformation(
                "بکاپ بعدی در ساعت {NextRun:yyyy/MM/dd HH:mm:ss} اجرا خواهد شد (مدت انتظار: {Delay}).",
                next.Value, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await TryRunBackupAsync("Schedule", stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("سرویس زمان‌بندی بکاپ متوقف شد.");
    }

    private async Task CheckAndRunBackupIfMissingAsync(string reason, CancellationToken ct)
    {
        var cfg = _schedulerOptions.CurrentValue;
        if (!cfg.Enabled || cfg.Times is null || cfg.Times.Count == 0)
        {
            return;
        }

        var now = cfg.UseUtc ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var yesterday = today.AddDays(-1);

        var latestBackup = await GetLatestBackupFileAsync(ct);

        if (latestBackup is null)
        {
            _logger.LogWarning("هیچ فایل بکاپی یافت نشد. در حال اجرای بکاپ اضطراری...");
            await TryRunBackupAsync($"{reason} - NoBackupFound", ct);
            return;
        }

        bool needsBackup = false;

        var todaySchedulePassed = cfg.Times.Any(timeStr =>
        {
            if (string.IsNullOrWhiteSpace(timeStr)) return false;
            if (!TimeSpan.TryParseExact(timeStr.Trim(), SupportedTimeFormats, CultureInfo.InvariantCulture,
                    out var scheduledTime))
                return false;

            var scheduledDateTime = new DateTimeOffset(
                now.Year, now.Month, now.Day,
                scheduledTime.Hours, scheduledTime.Minutes, 0,
                now.Offset);

            return scheduledDateTime <= now;
        });

        if (todaySchedulePassed)
        {
            if (latestBackup.CreatedAtUtc < today)
            {
                needsBackup = true;
            }
        }

        if (latestBackup.CreatedAtUtc < yesterday)
        {
            _logger.LogWarning(
                "آخرین بکاپ ({LatestBackupDate:yyyy/MM/dd HH:mm:ss}) قدیمی‌تر از شروع روز گذشته است. در حال اجرای بکاپ جبرانی...",
                latestBackup.CreatedAtUtc);
            needsBackup = true;
        }

        if (needsBackup)
        {
            _logger.LogWarning("فقدان بکاپ برای روزهای اخیر شناسایی شد. در حال اجرای بکاپ جبرانی...");
            await TryRunBackupAsync($"{reason} - MissingBackup", ct);
        }
        else
        {
            _logger.LogDebug("بکاپ‌های اخیر موجود است. نیازی به بکاپ جبرانی نیست.");
        }
    }

    private async Task<BackupFileDto?> GetLatestBackupFileAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
        var backupFiles = await backupService.GetBackupFilesAsync(ct);

        return backupFiles.FirstOrDefault();
    }

    private async Task TryRunBackupAsync(string reason, CancellationToken ct)
    {
        if (!await _lock.WaitAsync(0, ct))
        {
            _logger.LogWarning("عملیات بکاپ‌گیری به دلیل اجرای همزمان بکاپ دیگر رد شد.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

            var schedulerCfg = _schedulerOptions.CurrentValue;
            var storageCfg = _storageOptions.CurrentValue;

            var targetPath = !string.IsNullOrWhiteSpace(schedulerCfg.BackupPathOverride)
                ? schedulerCfg.BackupPathOverride
                : storageCfg.BackupPath;

            _logger.LogInformation("شروع بکاپ خودکار دیتابیس. علت: {Reason} | مسیر مقصد: {Path}", reason, targetPath);

            var result = await backupService.CreateBackupAsync(targetPath, ct);

            if (result.IsSuccess)
            {
                _logger.LogInformation("بکاپ زمان‌بندی‌شده با موفقیت ایجاد شد: {FileName} ({Size} بایت)",
                    result.FileName, result.SizeInBytes);
            }
            else
            {
                _logger.LogError("خطا در ایجاد بکاپ زمان‌بندی‌شده: {Message}", result.Message);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطای پیش‌بینی نشده در زمان اجرای سرویس بکاپ زمان‌بندی‌شده.");
        }
        finally
        {
            _lock.Release();
        }
    }

    private static DateTimeOffset? GetNextRun(DateTimeOffset now, List<string>? times)
    {
        if (times is null || times.Count == 0)
            return null;

        var candidates = new List<DateTimeOffset>();

        foreach (var timeStr in times)
        {
            if (string.IsNullOrWhiteSpace(timeStr))
                continue;

            if (!TimeSpan.TryParseExact(timeStr.Trim(), SupportedTimeFormats, CultureInfo.InvariantCulture,
                    out var timeOfDay))
                continue;

            var candidateToday = new DateTimeOffset(
                now.Year, now.Month, now.Day,
                timeOfDay.Hours, timeOfDay.Minutes, 0,
                now.Offset);

            if (candidateToday > now.AddSeconds(15))
            {
                candidates.Add(candidateToday);
            }
            else
            {
                var tomorrow = now.Date.AddDays(1);
                var candidateTomorrow = new DateTimeOffset(
                    tomorrow.Year, tomorrow.Month, tomorrow.Day,
                    timeOfDay.Hours, timeOfDay.Minutes, 0,
                    now.Offset);

                candidates.Add(candidateTomorrow);
            }
        }

        return candidates.Count > 0 ? candidates.Min() : null;
    }
}
