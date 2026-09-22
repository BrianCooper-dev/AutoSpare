using AutoSpare.Application.Common.Interfaces;
using AutoSpare.Application.Common.Settings;
using AutoSpare.Infrastructure.Persistence;
using AutoSpare.Infrastructure.Persistence.Seed;
using AutoSpare.Web.Components;
using Microsoft.AspNetCore.Authentication;
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

    builder.Services
        .AddSingleton<IPasswordHasher, AutoSpare.Infrastructure.Services.PasswordHasherService>();

    // -------------------------------------------------------
    // Blazor UI Services
    // -------------------------------------------------------
    builder.Services
        .AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults
                .AuthenticationScheme;
            options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults
                .AuthenticationScheme;
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

    // -------------------------------------------------------
    // Database (Infrastructure)
    // -------------------------------------------------------
    var connectionString =
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Connection string 'DefaultConnection' was not found.");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));

    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    var app = builder.Build();

    // -------------------------------------------------------
    // Database Migration & Seeding
    // -------------------------------------------------------
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var dbContext = services.GetRequiredService<ApplicationDbContext>();
            var seederLogger = services.GetRequiredService<ILogger<Program>>();
            var passwordHasher = services.GetRequiredService<IPasswordHasher>();

            await DbInitializer.SeedAsync(dbContext, seederLogger, passwordHasher);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred during database migration or seeding.");
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
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseSerilogRequestLogging();
    app.UseAntiforgery();
    app.UseAuthentication();
    app.UseAuthorization();

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
        [Microsoft.AspNetCore.Mvc.FromForm] AutoSpare.Application.Models.LoginModel model,
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        HttpContext httpContext) =>
    {
        var normalizedUser = model.Username.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == normalizedUser);

        if (user is null || !passwordHasher.VerifyPassword(model.Password, user.PasswordHash))
        {
            return Results.Redirect("/login?error=InvalidCredentials");
        }

        // ثبت تاریخ آخرین ورود در دامین
        user.RecordLogin();
        await dbContext.SaveChangesAsync();

        // ایجاد Claims و کوکی
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(System.Security.Claims.ClaimTypes.Name, user.Username),
            new("FullName", user.FullName)
        };

        var identity = new System.Security.Claims.ClaimsIdentity(
            claims,
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var authProperties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : null
        };

        await httpContext.SignInAsync(
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProperties);

        return Results.Redirect("/");
    }).DisableAntiforgery();

    app.MapGet("/logout", async (HttpContext httpContext) =>
    {
        await httpContext.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults
            .AuthenticationScheme);
        return Results.Redirect("/login");
    });

    // -------------------------------------------------------
    // Storage directories preparation
    // -------------------------------------------------------
    var storageSettings = app.Services
        .GetRequiredService<IOptions<StorageSettings>>()
        .Value;

    var webRootPath = app.Environment.WebRootPath
                      ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");

    var imagesFullPath = Path.IsPathRooted(storageSettings.ImagesPath)
        ? Path.GetFullPath(storageSettings.ImagesPath)
        : Path.GetFullPath(Path.Combine(webRootPath, storageSettings.ImagesPath));

    var backupFullPath = Path.IsPathRooted(storageSettings.BackupPath)
        ? Path.GetFullPath(storageSettings.BackupPath)
        : Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, storageSettings.BackupPath));

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
    Log.Fatal(exception, "AutoSpare Web Application terminated unexpectedly.");
}
finally
{
    await Log.CloseAndFlushAsync();
}
