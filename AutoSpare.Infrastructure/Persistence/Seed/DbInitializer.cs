using AutoSpare.Application.Common.Interfaces;
using AutoSpare.Domain.Users;
using AutoSpare.Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoSpare.Infrastructure.Persistence.Seed;

public static class DbInitializer
{
    public static async Task SeedAsync(
        ApplicationDbContext context,
        ILogger logger,
        IPasswordHasher passwordHasher,
        CancellationToken cancellationToken = default)
    {
        // مایگریشن در Program.cs و پیش از فراخوانی SeedAsync اجرا می‌شود.

        // ایجاد انبارهای اولیه در صورت نبود انبار
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

            await context.Warehouses.AddRangeAsync(
                new[] { shopWarehouse, homeWarehouse },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Default warehouses seeded successfully.");
        }

        // سیستم تک‌کاربره: فقط وقتی هیچ کاربری وجود ندارد، مدیر اولیه ساخته شود.
        if (!await context.Users.AnyAsync(cancellationToken))
        {
            const string defaultUsername = "admin";
            const string defaultPassword = "Admin@123456";

            var hashedPassword =
                passwordHasher.HashPassword(defaultPassword);

            var adminUser = new AppUser(
                fullName: "مدیر سیستم",
                username: defaultUsername,
                passwordHash: hashedPassword,
                quickPinHash: null
            );

            await context.Users.AddAsync(
                adminUser,
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Default Admin user created successfully.");
        }
    }
}
