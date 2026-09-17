using AutoSpare.Domain.Users;
using AutoSpare.Domain.Warehouses;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoSpare.Infrastructure.Persistence.Seed;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        // 1. اطمینان از اعمال کامل مایگریشن‌ها
        if ((await context.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            await context.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Pending migrations applied successfully.");
        }

        // 2. ایجاد انبارهای اولیه در صورت عدم وجود
        if (!await context.Warehouses.AnyAsync(cancellationToken))
        {
            var shopWarehouse = new Warehouse(
                Warehouse.ShopWarehouseId,
                name: "انبار مغازه",
                code: "SHOP-01",
                address: "فروشگاه اصلی"
            );

            var homeWarehouse = new Warehouse(
                Warehouse.HomeWarehouseId,
                name: "انبار منزل",
                code: "HOME-01",
                address: "انبار پشتیبان"
            );

            await context.Warehouses.AddRangeAsync(new[] { shopWarehouse, homeWarehouse }, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Default warehouses seeded successfully.");
        }

        // 3. ایجاد حساب کاربری مدیر اولیه در صورت عدم وجود
        const string adminUsername = "admin";
        var adminExists = await context.Users.AnyAsync(u => u.Username == adminUsername, cancellationToken);

        if (!adminExists)
        {
            // استفاده از PasswordHasher استاندارد ASP.NET Core برای امنیت بالا (PBKDF2)
            var hasher = new PasswordHasher<AppUser>();

            // نمونه‌سازی موقت برای ایجاد Hash معتبر
            // رمز پیش‌فرض اولیه: Admin@123456
            var dummyUser = new AppUser("مدیر سیستم", adminUsername, "TEMP_HASH");
            var hashedPassword = hasher.HashPassword(dummyUser, "Admin@123456");

            var adminUser = new AppUser(
                fullName: "مدیر سیستم",
                username: adminUsername,
                passwordHash: hashedPassword,
                quickPinHash: null
            );

            await context.Users.AddAsync(adminUser, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Default Admin user created successfully (Username: admin, Password: Admin@123456).");
        }
    }
}
