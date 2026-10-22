using System.Globalization;
using AutoSpare.Application.Backup;
using AutoSpare.Application.Common.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

namespace AutoSpare.Infrastructure.Services;

public sealed class ScheduledBackupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<BackupSchedulerSettings> _schedulerOptions;
    private readonly IOptionsMonitor<StorageSettings> _storageOptions;

    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly string[] SupportedTimeFormats = ["hh\\:mm", "h\\:mm", "HH\\:mm", "H\\:mm"];

    public ScheduledBackupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<BackupSchedulerSettings> schedulerOptions,
        IOptionsMonitor<StorageSettings> storageOptions)
    {
        _scopeFactory = scopeFactory;
        _schedulerOptions = schedulerOptions;
        _storageOptions = storageOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("سرویس زمان‌بندی بکاپ پایگاه داده راه‌اندازی شد.");

        var initialCfg = _schedulerOptions.CurrentValue;
        if (initialCfg.RunOnStartup && initialCfg.Enabled)
        {
            _ = Task.Run(() => TryRunBackupAsync("Startup", stoppingToken), stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var cfg = _schedulerOptions.CurrentValue;

            if (!cfg.Enabled)
            {
                // اگر غیرفعال است هر ۵ دقیقه مجدداً چک می‌کند تا در صورت فعال‌سازی از کانفیگ کار را آغاز کند
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                continue;
            }

            var now = cfg.UseUtc ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
            var next = GetNextRun(now, cfg.Times);

            if (next is null)
            {
                Log.Warning("هیچ ساعت زمان‌بندی معتبری در BackupScheduler:Times یافت نشد. تلاش مجدد تا ۱۰ دقیقه دیگر.");
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                continue;
            }

            var delay = next.Value - now;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            Log.Information("بکاپ بعدی در ساعت {NextRun:yyyy/MM/dd HH:mm:ss} اجرا خواهد شد (مدت انتظار: {Delay}).",
                next.Value, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // اجرای بکاپ در سررسید
            await TryRunBackupAsync("Schedule", stoppingToken);

            // توقف کوتاه چند ثانیه‌ای تا از trigger مکرر در همان دقیقه جلوگیری شود
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        Log.Information("سرویس زمان‌بندی بکاپ متوقف شد.");
    }

    private async Task TryRunBackupAsync(string reason, CancellationToken ct)
    {
        if (!await _lock.WaitAsync(0, ct))
        {
            Log.Warning("عملیات بکاپ‌گیری به دلیل اجرای همزمان بکاپ دیگر رد شد.");
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

            Log.Information("شروع بکاپ خودکار دیتابیس. علت: {Reason} | مسیر مقصد: {Path}", reason, targetPath);

            var result = await backupService.CreateBackupAsync(targetPath, ct);

            if (result.IsSuccess)
            {
                Log.Information("بکاپ زمان‌بندی‌شده با موفقیت ایجاد شد: {FileName} ({Size} بایت)",
                    result.FileName, result.SizeInBytes);
            }
            else
            {
                Log.Error("خطا در ایجاد بکاپ زمان‌بندی‌شده: {Message}", result.Message);
            }
        }
        catch (OperationCanceledException)
        {
            // بسته‌شدن طبیعی هاست در زمان خاموشی برنامه
        }
        catch (Exception ex)
        {
            Log.Error(ex, "خطای پیش‌بینی نشده در زمان اجرای سرویس بکاپ زمان‌بندی‌شده.");
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

            // کاندیدای امروز
            var candidateToday = new DateTimeOffset(
                now.Year, now.Month, now.Day,
                timeOfDay.Hours, timeOfDay.Minutes, 0,
                now.Offset);

            // اگر حداقل ۲۰ ثانیه از زمان حال فاصله دارد در کاندیداهای امروز قرار بگیرد
            if (candidateToday > now.AddSeconds(15))
            {
                candidates.Add(candidateToday);
            }
            else
            {
                // در غیر این صورت زمان مربوط به فردا است
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
