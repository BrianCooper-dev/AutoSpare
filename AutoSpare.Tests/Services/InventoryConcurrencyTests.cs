using AutoSpare.Application.Sales.DTOs;
using AutoSpare.Domain.Brands;
using AutoSpare.Domain.Categories;
using AutoSpare.Domain.Inventories;
using AutoSpare.Domain.Inventories.Enums;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Warehouses;
using AutoSpare.Infrastructure.Persistence;
using AutoSpare.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AutoSpare.Tests.Services;

public class InventoryConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public InventoryConcurrencyTests()
    {
        // استفاده از دیتابیس اشتراکی در حافظه با فعال‌سازی Timeout برای مدیریت همزمانی
        _connection = new SqliteConnection("DataSource=file:memdb1?mode=memory&cache=shared;Default Timeout=30;");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ApplicationDbContext(_options);
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task DomainInventory_ConcurrentDecreases_MustNeverResultInNegativeStock()
    {
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid(), initialQuantity: 10);
        var attempts = 30;
        var decrementAmount = 1;

        var tasks = Enumerable.Range(0, attempts).Select(_ => Task.Run(() =>
        {
            try
            {
                lock (inventory)
                {
                    inventory.DecreaseQuantity(
                        amount: decrementAmount,
                        type: StockMovementType.Sale,
                        reason: "تست همزمانی");
                }

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        })).ToArray();

        var results = await Task.WhenAll(tasks);
        var successfulDecreases = results.Count(r => r);

        Assert.Equal(10, successfulDecreases);
        Assert.Equal(0, inventory.Quantity);
    }

    [Fact]
    public async Task SaleService_ConcurrentSales_StockShouldRemainValid()
    {
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var brandId = Guid.NewGuid();

        await using (var seedContext = new ApplicationDbContext(_options))
        {
            // ایجاد دسته‌بندی و برند برای رعایت محدودیت‌های کلید خارجی (Foreign Key)
            var category = new Category("دسته‌بندی تستی", "CatTest");
            typeof(Category).GetProperty(nameof(Category.Id))?.SetValue(category, categoryId);
            await seedContext.Categories.AddAsync(category);

            var brand = new Brand("برند تستی", "BrandTest");
            typeof(Brand).GetProperty(nameof(Brand.Id))?.SetValue(brand, brandId);
            await seedContext.Brands.AddAsync(brand);

            var warehouse = new Warehouse("انبار تستی", "تهران");
            typeof(Warehouse).GetProperty(nameof(Warehouse.Id))?.SetValue(warehouse, warehouseId);
            await seedContext.Warehouses.AddAsync(warehouse);

            var product = new Product(
                name: "قطعه تستی",
                internalCode: "PRD-TEST-001",
                model: "Part-101",
                purchasePrice: 10_000m,
                salePrice: 15_000m,
                categoryId: categoryId,
                brandId: brandId,
                imagePath: null,
                defaultWarehouseId: warehouseId);

            typeof(Product).GetProperty(nameof(Product.Id))?.SetValue(product, productId);
            await seedContext.Products.AddAsync(product);

            var inventory = new Inventory(productId, warehouseId, initialQuantity: 5);
            await seedContext.Inventories.AddAsync(inventory);

            await seedContext.SaveChangesAsync();
        }

        var runSale = async (string invoiceNum) =>
        {
            await using var context = new ApplicationDbContext(_options);
            var loggerMock = new Mock<ILogger<SaleService>>();
            var service = new SaleService(context, loggerMock.Object);

            var dto = new CreateSaleDto
            {
                InvoiceNumber = invoiceNum,
                CustomerName = "مشتری همزمان",
                WarehouseId = warehouseId,
                FinalizeImmediately = true,
                Items = new List<CreateSaleItemDto>
                {
                    new()
                    {
                        ProductId = productId,
                        WarehouseId = warehouseId,
                        Quantity = 4,
                        UnitPrice = 15_000m
                    }
                }
            };

            try
            {
                await service.CreateSaleAsync(dto);
                return true;
            }
            catch
            {
                return false;
            }
        };

        var task1 = Task.Run(() => runSale("INV-CONC-001"));
        var task2 = Task.Run(() => runSale("INV-CONC-002"));

        var results = await Task.WhenAll(task1, task2);

        await using (var verifyContext = new ApplicationDbContext(_options))
        {
            var finalInventory = await verifyContext.Inventories.FirstAsync(i => i.ProductId == productId);

            Assert.True(finalInventory.Quantity >= 0,
                $"موجودی نباید منفی باشد. موجودی فعلی: {finalInventory.Quantity}");

            var successCount = results.Count(s => s);
            Assert.True(successCount <= 1, $"حداکثر یک فاکتور می‌تواند نهایی شود اما {successCount} فاکتور ثبت شد.");
        }
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
