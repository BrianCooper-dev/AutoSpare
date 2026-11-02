using AutoSpare.Application.Sales.DTOs;
using AutoSpare.Domain.Inventories;
using AutoSpare.Domain.Inventories.Enums;
using AutoSpare.Domain.Products;
using AutoSpare.Infrastructure.Persistence;
using AutoSpare.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AutoSpare.Tests.Services;

public class InventoryConcurrencyTests
{
    private static DbContextOptions<ApplicationDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
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
        var options = CreateInMemoryOptions();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var brandId = Guid.NewGuid();

        await using (var seedContext = new ApplicationDbContext(options))
        {
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
            await using var context = new ApplicationDbContext(options);
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

        await using (var verifyContext = new ApplicationDbContext(options))
        {
            var finalInventory = await verifyContext.Inventories.FirstAsync(i => i.ProductId == productId);

            Assert.True(finalInventory.Quantity >= 0,
                $"موجودی نباید منفی باشد. موجودی فعلی: {finalInventory.Quantity}");

            var successCount = results.Count(s => s);
            Assert.True(successCount <= 1, $"حداکثر یک فاکتور می‌تواند نهایی شود اما {successCount} فاکتور ثبت شد.");
        }
    }
}
