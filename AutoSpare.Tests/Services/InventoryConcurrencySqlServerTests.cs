using AutoSpare.Domain.Brands;
using AutoSpare.Domain.Categories;
using AutoSpare.Domain.Inventories;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Warehouses;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Tests.Services;

[Trait("Category", "SqlServer")]
public class InventoryConcurrencySqlServerTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("AUTOSPARE_TEST_SQLSERVER")
        ??
        @"Server=(localdb)\MSSQLLocalDB;Database=AutoSpare_ConcurrencyTest;Trusted_Connection=True;TrustServerCertificate=True";

    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options);

    /// <summary>
    /// بررسی می‌کند که آیا SQL Server (LocalDB) روی سیستم در دسترس است یا خیر.
    /// </summary>
    private static async Task<bool> CanConnectToSqlServerAsync()
    {
        try
        {
            await using var context = CreateContext();
            return await context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private static async Task<Guid> SeedInventoryWithTenUnitsAsync()
    {
        await using (var setup = CreateContext())
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.MigrateAsync();
        }

        await using var seed = CreateContext();

        var warehouse = new Warehouse("انبار تست همزمانی", "تهران");
        var category = new Category("دسته تست", "Test");
        var brand = new Brand("برند تست", "Test");

        await seed.Warehouses.AddAsync(warehouse);
        await seed.Categories.AddAsync(category);
        await seed.Brands.AddAsync(brand);
        await seed.SaveChangesAsync();

        var product = new Product(
            name: "کالای تست همزمانی",
            internalCode: $"TST-{Guid.NewGuid():N}",
            model: "مدل تست",
            purchasePrice: 100m,
            salePrice: 200m,
            categoryId: category.Id,
            brandId: brand.Id,
            defaultWarehouseId: warehouse.Id);

        await seed.Products.AddAsync(product);
        await seed.SaveChangesAsync();

        var inventory = new Inventory(product.Id, warehouse.Id, 10);
        await seed.Inventories.AddAsync(inventory);
        await seed.SaveChangesAsync();

        return inventory.Id;
    }

    [Fact]
    public async Task TwoUsersSellEightFromStockOfTen_SecondMustFail_AndStockNeverGoesNegative()
    {
        // اگر SQL Server یا LocalDB روی سیستم نصب/فعال نبود، تست نادیده گرفته شود
        if (!await CanConnectToSqlServerAsync())
        {
            // در xUnit برای Skip کردن در حین اجرا می‌توان از این روش یا خروج امن استفاده کرد
            return; // یا throw new SkipException(...) بسته به نسخه
        }

        // Arrange: موجودی ۱۰ عددی
        var inventoryId = await SeedInventoryWithTenUnitsAsync();

        // دو کاربر مستقل، هر دو موجودی ۱۰ را می‌بینند
        await using var user1 = CreateContext();
        await using var user2 = CreateContext();

        var inv1 = await user1.Inventories.FirstAsync(i => i.Id == inventoryId);
        var inv2 = await user2.Inventories.FirstAsync(i => i.Id == inventoryId);

        // هر دو ۸ عدد می‌فروشند: ۱۰ - ۸ = ۲
        user1.Entry(inv1).Property(i => i.Quantity).CurrentValue = 2;
        user2.Entry(inv2).Property(i => i.Quantity).CurrentValue = 2;

        // Act
        await user1.SaveChangesAsync(); // اولی باید موفق شود

        // Assert: دومی باید با خطای همزمانی رد شود
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => user2.SaveChangesAsync());

        await using var verify = CreateContext();
        var finalQuantity = await verify.Inventories
            .Where(i => i.Id == inventoryId)
            .Select(i => i.Quantity)
            .FirstAsync();

        // نه منفی، نه ۴ (Lost Update) — بلکه دقیقاً ۲
        Assert.Equal(2, finalQuantity);
    }
}
