using System.Globalization;
using AutoSpare.Application.Backup;
using AutoSpare.Application.Brands;
using AutoSpare.Application.Categories;
using AutoSpare.Application.Common.Interfaces;
using AutoSpare.Application.Common.Settings;
using AutoSpare.Application.DailySettlements;
using AutoSpare.Application.Products;
using AutoSpare.Application.Purchases;
using AutoSpare.Application.Reports;
using AutoSpare.Application.Sales;
using AutoSpare.Application.Settings;
using AutoSpare.Application.Suppliers;
using AutoSpare.Infrastructure.Persistence;
using AutoSpare.Infrastructure.Persistence.Seed;
using AutoSpare.Infrastructure.Services;
using AutoSpare.Web.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MudBlazor;
using MudBlazor.Services;
using Serilog;

// ثبت خطاهایی که پیش از تکمیل راه‌اندازی رخ می‌دهند.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting AutoSpare Web Application...");

    // در نسخه پابلیش‌شده، appsettings و wwwroot از کنار فایل اجرایی خوانده شوند.
    // در Development، ساختار معمول پروژه حفظ می‌شود.
    var environmentName =
        Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    var contentRootPath = string.Equals(
        environmentName,
        Environments.Development,
        StringComparison.OrdinalIgnoreCase)
        ? Directory.GetCurrentDirectory()
        : AppContext.BaseDirectory;

    var builder = WebApplication.CreateBuilder(
        new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = contentRootPath
        });

    // -------------------------------------------------------
    // Logging (Serilog)
    // -------------------------------------------------------
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

    // -------------------------------------------------------
    // Application settings
    // -------------------------------------------------------
    builder.Services
        .AddOptions<StorageSettings>()
        .Bind(builder.Configuration.GetRequiredSection(StorageSettings.SectionName))
        .Validate(
            settings => !string.IsNullOrWhiteSpace(settings.ImagesPath),
            $"{StorageSettings.SectionName}:ImagesPath is required.")
        .Validate(
            settings => !string.IsNullOrWhiteSpace(settings.BackupPath),
            $"{StorageSettings.SectionName}:BackupPath is required.")
        .Validate(
            settings => settings.MaxImageSizeInMb > 0,
            $"{StorageSettings.SectionName}:MaxImageSizeInMb must be greater than zero.")
        .ValidateOnStart();

    builder.Services.AddSingleton<
        IPasswordHasher,
        AutoSpare.Infrastructure.Services.PasswordHasherService>();

    builder.Services.AddScoped<
        ICategoryService,
        AutoSpare.Infrastructure.Services.CategoryService>();

    builder.Services.AddScoped<IBrandService, BrandService>();
    builder.Services.AddScoped<ISupplierService, SupplierService>();
    builder.Services.AddScoped<IProductService, ProductService>();
    builder.Services.AddScoped<IFileStorageService, FileStorageService>();
    builder.Services.AddScoped<IPurchaseService, PurchaseService>();
    builder.Services.AddScoped<ISaleService, SaleService>();
    builder.Services.AddScoped<IDailySettlementService, DailySettlementService>();
    builder.Services.AddScoped<IReportService, ReportService>();
    builder.Services.AddScoped<IBackupService, BackupService>();
    builder.Services.AddScoped<ISettingService, SettingService>();

    builder.Services
        .AddOptions<BackupSchedulerSettings>()
        .Bind(builder.Configuration.GetRequiredSection(
            BackupSchedulerSettings.SectionName))
        .ValidateOnStart();

    // توجه: پیاده‌سازی این سرویس باید با SQLite سازگار شده باشد.
    builder.Services.AddHostedService<ScheduledBackupHostedService>();

    // -------------------------------------------------------
    // Blazor UI Services
    // -------------------------------------------------------
    builder.Services
        .AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddCascadingAuthenticationState();

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme =
                CookieAuthenticationDefaults.AuthenticationScheme;

            options.DefaultChallengeScheme =
                CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.Cookie.Name = "AutoSpare.Auth";
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            options.AccessDeniedPath = "/access-denied";
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;
        });

    builder.Services.AddAuthorization();

    builder.Services.AddMudServices(config =>
    {
        config.SnackbarConfiguration.PositionClass =
            Defaults.Classes.Position.BottomLeft;

        config.SnackbarConfiguration.PreventDuplicates = true;
        config.SnackbarConfiguration.NewestOnTop = true;
        config.SnackbarConfiguration.ShowCloseIcon = true;
        config.SnackbarConfiguration.VisibleStateDuration = 4000;
        config.SnackbarConfiguration.HideTransitionDuration = 300;
        config.SnackbarConfiguration.ShowTransitionDuration = 300;
        config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
    });

    // -------------------------------------------------------
    // Database (SQLite)
    // -------------------------------------------------------
    var configuredConnectionString =
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Connection string 'DefaultConnection' was not found.");

    var sqliteConnection = new SqliteConnectionStringBuilder(
        configuredConnectionString);

    if (string.IsNullOrWhiteSpace(sqliteConnection.DataSource)
        || sqliteConnection.DataSource == ":memory:")
    {
        throw new InvalidOperationException(
            "DefaultConnection must specify a SQLite database file.");
    }

    var databasePath = Path.IsPathRooted(sqliteConnection.DataSource)
        ? Path.GetFullPath(sqliteConnection.DataSource)
        : Path.GetFullPath(Path.Combine(
            builder.Environment.ContentRootPath,
            sqliteConnection.DataSource));

    Directory.CreateDirectory(
        Path.GetDirectoryName(databasePath)
        ?? throw new InvalidOperationException(
            "The SQLite database directory could not be resolved."));

    sqliteConnection.DataSource = databasePath;

    builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
        options.UseSqlite(sqliteConnection.ToString()));

    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    var app = builder.Build();

    // -------------------------------------------------------
    // Storage directories preparation
    // -------------------------------------------------------
    var storageSettings = app.Services
        .GetRequiredService<IOptions<StorageSettings>>()
        .Value;

    var webRootPath = app.Environment.WebRootPath
                      ?? Path.Combine(
                          app.Environment.ContentRootPath,
                          "wwwroot");

    var imagesFullPath = Path.IsPathRooted(storageSettings.ImagesPath)
        ? Path.GetFullPath(storageSettings.ImagesPath)
        : Path.GetFullPath(Path.Combine(
            webRootPath,
            storageSettings.ImagesPath));

    var backupFullPath = Path.IsPathRooted(storageSettings.BackupPath)
        ? Path.GetFullPath(storageSettings.BackupPath)
        : Path.GetFullPath(Path.Combine(
            app.Environment.ContentRootPath,
            storageSettings.BackupPath));

    Directory.CreateDirectory(imagesFullPath);
    Directory.CreateDirectory(backupFullPath);

    Log.Information(
        "Storage directories are ready. ImagesPath: {ImagesPath}, BackupPath: {BackupPath}",
        imagesFullPath,
        backupFullPath);

    Log.Information(
        "SQLite database path: {DatabasePath}",
        databasePath);

    // -------------------------------------------------------
    // Database Migration & Seeding
    // -------------------------------------------------------
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;

        try
        {
            var dbContext =
                services.GetRequiredService<ApplicationDbContext>();

            var seederLogger =
                services.GetRequiredService<ILogger<Program>>();

            var passwordHasher =
                services.GetRequiredService<IPasswordHasher>();

            await dbContext.Database.MigrateAsync();

            await DbInitializer.SeedAsync(
                dbContext,
                seederLogger,
                passwordHasher);
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                "An error occurred during SQLite migration or seeding.");

            throw;
        }
    }

    // -------------------------------------------------------
    // HTTP pipeline
    // -------------------------------------------------------
    if (app.Environment.IsDevelopment())
    {
        app.UseMigrationsEndPoint();
    }
    else
    {
        app.UseExceptionHandler(
            "/Error",
            createScopeForErrors: true);

        app.UseHsts();
    }

    var defaultCulture = new CultureInfo("fa-IR");
    defaultCulture.NumberFormat.NumberDecimalSeparator = ".";
    defaultCulture.NumberFormat.CurrencyDecimalSeparator = ".";

    var localizationOptions = new RequestLocalizationOptions
    {
        DefaultRequestCulture = new RequestCulture(defaultCulture),
        SupportedCultures = new List<CultureInfo> { defaultCulture },
        SupportedUICultures = new List<CultureInfo> { defaultCulture }
    };

    app.UseRequestLocalization(localizationOptions);

    app.UseStatusCodePagesWithReExecute(
        "/not-found",
        createScopeForStatusCodePages: true);

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseSerilogRequestLogging();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    // -------------------------------------------------------
    // Endpoints
    // -------------------------------------------------------
    app.MapStaticAssets();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // -------------------------------------------------------
    // Auth Endpoints (Login / Logout)
    // -------------------------------------------------------
    app.MapPost("/api/auth/login", async (
        [Microsoft.AspNetCore.Mvc.FromForm]
            AutoSpare.Application.Models.LoginModel model,
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        HttpContext httpContext) =>
    {
        var normalizedUser =
            model.Username.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u =>
                u.Username == normalizedUser);

        if (user is null
            || !passwordHasher.VerifyPassword(
                model.Password,
                user.PasswordHash))
        {
            return Results.Redirect(
                "/login?error=InvalidCredentials");
        }

        user.RecordLogin();
        await dbContext.SaveChangesAsync();

        var claims = new List<System.Security.Claims.Claim>
        {
            new(
                System.Security.Claims.ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new(
                System.Security.Claims.ClaimTypes.Name,
                user.Username),

            new("FullName", user.FullName)
        };

        var identity = new System.Security.Claims.ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal =
            new System.Security.Claims.ClaimsPrincipal(identity);

        var authProperties =
            new Microsoft.AspNetCore.Authentication.AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(7)
                    : null
            };

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProperties);

        return Results.Redirect("/");
    }).DisableAntiforgery();

    app.MapPost("/api/auth/logout", async (HttpContext context) =>
    {
        await context.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return Results.Redirect("/login");
    });

    // فقط یک‌بار ثبت می‌شود؛ در کد قبلی دوبار وجود داشت.
    app.MapGet("/logout", async (HttpContext context) =>
    {
        await context.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return Results.Redirect("/login");
    });

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(
        exception,
        "AutoSpare Web Application terminated unexpectedly.");
}
finally
{
    await Log.CloseAndFlushAsync();
}
