using AutoSpare.Application.Common.Settings;
using AutoSpare.Web.Components;
using AutoSpare.Web.Components.Account;
using AutoSpare.Web.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

// Bootstrap logger:
// برای ثبت خطاهایی که قبل از تکمیل راه‌اندازی برنامه رخ می‌دهند.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting AutoSpare Web Application...");

    var builder = WebApplication.CreateBuilder(args);

    // -------------------------------------------------------
    // Logging
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
        .Bind(builder.Configuration.GetRequiredSection(
            StorageSettings.SectionName))
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

    // -------------------------------------------------------
    // Blazor
    // -------------------------------------------------------

    builder.Services
        .AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddCascadingAuthenticationState();

    builder.Services.AddScoped<IdentityRedirectManager>();

    builder.Services.AddScoped<
        AuthenticationStateProvider,
        IdentityRevalidatingAuthenticationStateProvider>();

    // -------------------------------------------------------
    // Database
    // -------------------------------------------------------

    var connectionString =
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Connection string 'DefaultConnection' was not found.");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));

    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    // -------------------------------------------------------
    // Authentication and Identity
    // -------------------------------------------------------

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme =
                IdentityConstants.ApplicationScheme;

            options.DefaultSignInScheme =
                IdentityConstants.ExternalScheme;
        })
        .AddIdentityCookies();

    builder.Services
        .AddIdentityCore<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = true;
            options.Stores.SchemaVersion =
                IdentitySchemaVersions.Version3;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

    builder.Services.AddSingleton<
        IEmailSender<ApplicationUser>,
        IdentityNoOpEmailSender>();

    var app = builder.Build();

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

    app.UseStatusCodePagesWithReExecute(
        "/not-found",
        createScopeForStatusCodePages: true);

    app.UseHttpsRedirection();

    // برای سرو شدن فایل‌هایی که هنگام اجرای برنامه
    // در wwwroot آپلود می‌شوند.
    app.UseStaticFiles();

    app.UseSerilogRequestLogging();

    app.UseAntiforgery();

    // -------------------------------------------------------
    // Endpoints
    // -------------------------------------------------------

    app.MapStaticAssets();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.MapAdditionalIdentityEndpoints();

    // -------------------------------------------------------
    // Storage directories
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
        : Path.GetFullPath(
            Path.Combine(webRootPath, storageSettings.ImagesPath));

    var backupFullPath = Path.IsPathRooted(storageSettings.BackupPath)
        ? Path.GetFullPath(storageSettings.BackupPath)
        : Path.GetFullPath(
            Path.Combine(
                app.Environment.ContentRootPath,
                storageSettings.BackupPath));

    Directory.CreateDirectory(imagesFullPath);
    Directory.CreateDirectory(backupFullPath);

    Log.Information(
        "Storage directories are ready. ImagesPath: {ImagesPath}, BackupPath: {BackupPath}",
        imagesFullPath,
        backupFullPath);

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
