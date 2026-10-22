using AutoSpare.Application.Common.Settings;
using AutoSpare.Domain.Brands;
using AutoSpare.Domain.Categories;
using AutoSpare.Domain.Products;
using AutoSpare.Infrastructure.Persistence;
using AutoSpare.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AutoSpare.Tests.Services;

[Collection("DatabaseRestoreTests")]
public class DatabaseRestoreIntegrationTests : IAsyncLifetime
{
    private const string BaseServerConnString =
        "Server=.;Database=master;Trusted_Connection=True;TrustServerCertificate=True";

    private readonly string _sourceDbName = $"AutoSpare_Source_{Guid.NewGuid():N}";
    private readonly string _targetTestDbName = $"AutoSpare_Test_{Guid.NewGuid():N}";

    // استفاده از یک مسیر مشخص در درایو C که سرویس SQL Server قطعاً به آن دسترسی کامل دارد
    private readonly string _backupFolder = @"C:\AutoSpare_Restore_Tests";

    private string SourceConnString =>
        $"Server=.;Database={_sourceDbName};Trusted_Connection=True;TrustServerCertificate=True";

    private string TargetTestConnString =>
        $"Server=.;Database={_targetTestDbName};Trusted_Connection=True;TrustServerCertificate=True";

    public async Task InitializeAsync()
    {
        if (!Directory.Exists(_backupFolder))
        {
            Directory.CreateDirectory(_backupFolder);
        }

        // ایجاد دیتابیس منبع با استفاده از EF Core
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(SourceConnString)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task RestoreBackup_ToTestDatabase_ShouldRestoreAccurateData()
    {
        // -------------------------------------------------------------
        // ۱. Arrange: درج داده‌های آزمایشی در دیتابیس منبع
        // -------------------------------------------------------------
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(SourceConnString)
            .Options;

        var uniqueProductCode = $"TEST-RESTORE-{Random.Shared.Next(1000, 9999)}";
        var sampleCategory = new Category("سیستم تعلیق آزمایشی");
        var sampleBrand = new Brand("برند آزمایشی");

        await using (var seedContext = new ApplicationDbContext(options))
        {
            await seedContext.Categories.AddAsync(sampleCategory);
            await seedContext.Brands.AddAsync(sampleBrand);
            await seedContext.SaveChangesAsync();

            var sampleProduct = new Product(
                name: "کمک فنر تست ریستور",
                internalCode: uniqueProductCode,
                model: "تست",
                purchasePrice: 1_200_000,
                salePrice: 1_500_000,
                categoryId: sampleCategory.Id,
                brandId: sampleBrand.Id
            );

            await seedContext.Products.AddAsync(sampleProduct);
            await seedContext.SaveChangesAsync();
        }

        // آماده‌سازی سرویس بکاپ متصل به دیتابیس منبع
        await using var dbContext = new ApplicationDbContext(options);
        var storageOptions = Options.Create(new StorageSettings
        {
            BackupPath = _backupFolder,
            ImagesPath = "Uploads"
        });

        var schedulerOptions = new MockOptionsMonitor<BackupSchedulerSettings>(new BackupSchedulerSettings
        {
            MaxBackupCount = 10,
            Enabled = true
        });

        var backupService = new BackupService(
            dbContext,
            storageOptions,
            schedulerOptions,
            NullLogger<BackupService>.Instance);

        // -------------------------------------------------------------
        // ۲. Act: تهیه بکاپ و سپس Restore روی دیتابیس تست جدید
        // -------------------------------------------------------------
        var backupResult = await backupService.CreateBackupAsync();

        // در صورت عدم موفقیت، پیام خطا را برای عیب‌یابی دقیق‌تر چاپ/لاگ می‌کنیم
        backupResult.IsSuccess.Should()
            .BeTrue($"ایجاد بکاپ از دیتابیس منبع باید موفقیت‌آمیز باشد. خطا: {backupResult.Message}");
        File.Exists(backupResult.FullPath).Should().BeTrue();

        var restoreResult = await backupService.RestoreToTestDatabaseAsync(
            backupResult.FullPath!,
            _targetTestDbName);

        // -------------------------------------------------------------
        // ۳. Assert: بررسی صحت عملیات و یکپارچگی داده‌ها (Data Integrity)
        // -------------------------------------------------------------
        restoreResult.IsSuccess.Should().BeTrue($"ریستور باید بدون خطا باشد: {restoreResult.Message}");

        // اتصال مستقیم به دیتابیس آزمایشی بازیابی‌شده و بررسی داده‌ها
        var targetOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(TargetTestConnString)
            .Options;

        await using (var targetContext = new ApplicationDbContext(targetOptions))
        {
            // الف) بررسی موجودیت جدول و اتصال سالم
            var canConnect = await targetContext.Database.CanConnectAsync();
            canConnect.Should().BeTrue("کانکشن به دیتابیس بازیابی‌شده باید برقرار باشد");

            // ب) اعتبارسنجی وجود رکورد ثبت‌شده قبل از بکاپ
            var restoredProduct = await targetContext.Products
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .FirstOrDefaultAsync(p => p.InternalCode == uniqueProductCode);

            restoredProduct.Should().NotBeNull("کالای درج‌شده در بکاپ باید در دیتابیس آزمایشی وجود داشته باشد");
            restoredProduct!.Name.Should().Be("کمک فنر تست ریستور");
            restoredProduct.SalePrice.Should().Be(1_500_000);
            restoredProduct.PurchasePrice.Should().Be(1_200_000);

            // ج) بررسی صحت روابط کلید خارجی (FK)
            restoredProduct.Category.Should().NotBeNull();
            restoredProduct.Category!.Name.Should().Be("سیستم تعلیق آزمایشی");
            restoredProduct.Brand.Should().NotBeNull();
            restoredProduct.Brand!.Name.Should().Be("برند آزمایشی");
        }
    }

    public async Task DisposeAsync()
    {
        // پاک‌سازی دیتابیس‌های موقت ایجاد شده در SQL Server
        await DropDatabaseAsync(_sourceDbName);
        await DropDatabaseAsync(_targetTestDbName);

        // حذف پوشه موقت تستی
        try
        {
            if (Directory.Exists(_backupFolder))
            {
                Directory.Delete(_backupFolder, true);
            }
        }
        catch
        {
            // نادیده گرفتن قفل احتمالی فایل
        }
    }

    private static async Task DropDatabaseAsync(string dbName)
    {
        try
        {
            await using var conn = new SqlConnection(BaseServerConnString);
            await conn.OpenAsync();

            var safeDb = dbName.Replace("]", "]]");
            var sql = $@"
                IF EXISTS (SELECT name FROM sys.databases WHERE name = '{safeDb}')
                BEGIN
                    ALTER DATABASE [{safeDb}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{safeDb}];
                END";

            await using var cmd = new SqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // نادیده گرفتن خطاهای احتمالی پاک‌سازی در صورت عدم وجود
        }
    }

    private sealed class MockOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public MockOptionsMonitor(T currentValue) => CurrentValue = currentValue;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
