using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AutoSpare.Web.Components;
using AutoSpare.Web.Components.Account;
using AutoSpare.Web.Data;
using Serilog;
using AutoSpare.Application.Common.Settings;


// ۱. کانفیگ لاگر اولیه برای ثبت خطاهای استارتاپ
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting AutoSpare Web Application...");

    var builder = WebApplication.CreateBuilder(args);

    // ۲. متصل کردن Serilog به Host پروژه
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ثبت تنظیمات StorageSettings در کانتینر DI با الگوی IOptions
    builder.Services.Configure<StorageSettings>(
        builder.Configuration.GetSection(StorageSettings.SectionName));


    // ۳. سرویس‌های Blazor
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddScoped<IdentityRedirectManager>();
    builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

    builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        })
        .AddIdentityCookies();

    // ۴. سرویس‌های دیتابیس و Identity
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                           throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));

    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    builder.Services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = true;
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

    builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

    var app = builder.Build();

    // ۵. لاگ گرفتن از درخواست‌های HTTP
    app.UseSerilogRequestLogging();

    // ۶. پایپ‌لاین و میدل‌ویرها
    if (app.Environment.IsDevelopment())
    {
        app.UseMigrationsEndPoint();
    }
    else
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();
    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // اندپوینت‌های مربوط به صفحات Identity
    app.MapAdditionalIdentityEndpoints();
    // اطمینان از وجود پوشه‌های آپلود و بکاپ در هنگام اجرای برنامه
    var storageConfig = app.Configuration.GetSection(StorageSettings.SectionName).Get<StorageSettings>()
                        ?? new StorageSettings();

    // مسیر کامل فیزیکی عکس‌ها در کنار پروژه
    var imagesFullPath = Path.Combine(app.Environment.ContentRootPath, storageConfig.ImagesPath);
    if (!Directory.Exists(imagesFullPath))
    {
        Directory.CreateDirectory(imagesFullPath);
        Log.Information("Created Images directory at: {Path}", imagesFullPath);
    }

    // مسیر فولدر بکاپ
    if (!Directory.Exists(storageConfig.BackupPath))
    {
        Directory.CreateDirectory(storageConfig.BackupPath);
        Log.Information("Created Backup directory at: {Path}", storageConfig.BackupPath);
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly!");
}
finally
{
    Log.CloseAndFlush();
}