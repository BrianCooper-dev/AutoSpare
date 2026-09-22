using AutoSpare.Application.Common.Interfaces;
using AutoSpare.Domain.Users;
using AutoSpare.Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoSpare.Infrastructure.Persistence.Seed;

public static class DbInitializer
{
    // پارامتر جدید: IPasswordHasher
    public static async Task SeedAsync(
        ApplicationDbContext context,
        ILogger logger,
        IPasswordHasher passwordHasher,
        CancellationToken cancellationToken = default)
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
            // ✅ هش کردن با واسط IPasswordHasher (اجرای تمیز و قابل تست)
            const string defaultPassword = "Admin@123456";
            var hashedPassword = passwordHasher.HashPassword(defaultPassword);

            var adminUser = new AppUser(
                fullName: "مدیر سیستم",
                username: adminUsername,
                passwordHash: hashedPassword,
                quickPinHash: null
            );

            await context.Users.AddAsync(adminUser, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Default Admin user created successfully (Username: admin)");
        }
    }
}
