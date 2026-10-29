using AutoSpare.Application.Sales.DTOs;
using AutoSpare.Domain.Brands;
using AutoSpare.Domain.Categories;
using AutoSpare.Domain.Inventories;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Sales.Enums;
using AutoSpare.Domain.Warehouses;
using AutoSpare.Infrastructure.Persistence;
using AutoSpare.Infrastructure.Services;
using AutoSpare.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Tests.Services;

public class SaleServiceTests
{
    private static DbContextOptions<ApplicationDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x =>
                x.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId
                        .TransactionIgnoredWarning))
            .Options;

    private static async Task<(Guid ProductId, Guid WarehouseId)> SeedBasicData(
        DbContextOptions<ApplicationDbContext> options,
        int initialStock)
    {
        await using var db = new TestApplicationDbContext(options);

        var warehouse = new Warehouse(
            "انبار مرکزی",
            "تهران",
            "02100000000");

        await db.Warehouses.AddAsync(warehouse);

        var category = new Category("لوازم یدکی", "Spare Parts");
        var brand = new Brand("ایران خودرو", "IKCO");

        await db.Categories.AddAsync(category);
        await db.Brands.AddAsync(brand);

        var product = new Product(
            name: "لنت ترمز",
            internalCode: "PAD-001",
            model: "پژو 206",
            purchasePrice: 200m,
            salePrice: 300m,
            categoryId: category.Id,
            brandId: brand.Id,
            imagePath: null,
            defaultWarehouseId: warehouse.Id
        );

        await db.Products.AddAsync(product);

        var inventory = new Inventory(
            product.Id,
            warehouse.Id,
            initialStock);

        await db.Inventories.AddAsync(inventory);
        await db.SaveChangesAsync();

        return (product.Id, warehouse.Id);
    }

    [Fact]
    public async Task CreateSale_WithFinalize_ShouldDecreaseInventory()
    {
        // Arrange
        var options = CreateOptions();
        var (productId, warehouseId) = await SeedBasicData(options, 10);

        var dto = new CreateSaleDto
        {
            InvoiceNumber = "INV-01",
            CustomerName = "علی رضایی",
            WarehouseId = warehouseId,
            FinalizeImmediately = true,
            Items = new List<CreateSaleItemDto>
            {
                new()
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    Quantity = 3,
                    UnitPrice = 300m
                }
            }
        };

        // Act
        Guid saleId;

        await using (var db = new TestApplicationDbContext(options))
        {
            var service = new SaleService(db);
            saleId = await service.CreateSaleAsync(dto);
        }

        // Assert: نتیجه را با Context جدید از دیتابیس تست بخوان.
        await using (var db = new TestApplicationDbContext(options))
        {
            var sale = await db.Sales
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == saleId);

            Assert.NotNull(sale);
            Assert.Equal(SaleStatus.Completed, sale.Status);

            var inventory = await db.Inventories
                .AsNoTracking()
                .FirstAsync(i =>
                    i.ProductId == productId &&
                    i.WarehouseId == warehouseId);

            Assert.Equal(7, inventory.Quantity);
        }
    }

    [Fact]
    public async Task CancelSale_ShouldRestoreInventory()
    {
        // Arrange
        var options = CreateOptions();
        var (productId, warehouseId) = await SeedBasicData(options, 10);

        Guid saleId;

        await using (var db = new TestApplicationDbContext(options))
        {
            var service = new SaleService(db);

            saleId = await service.CreateSaleAsync(new CreateSaleDto
            {
                InvoiceNumber = "INV-02",
                CustomerName = "علی رضایی",
                WarehouseId = warehouseId,
                FinalizeImmediately = true,
                Items = new List<CreateSaleItemDto>
                {
                    new()
                    {
                        ProductId = productId,
                        WarehouseId = warehouseId,
                        Quantity = 4,
                        UnitPrice = 300m
                    }
                }
            });
        }

        // موجودی پس از فروش: ۱۰ - ۴ = ۶
        await using (var db = new TestApplicationDbContext(options))
        {
            var inventoryBefore = await db.Inventories
                .AsNoTracking()
                .FirstAsync(i =>
                    i.ProductId == productId &&
                    i.WarehouseId == warehouseId);

            Assert.Equal(6, inventoryBefore.Quantity);
        }

        // Act
        await using (var db = new TestApplicationDbContext(options))
        {
            var service = new SaleService(db);
            await service.CancelSaleAsync(saleId, "انصراف مشتری");
        }

        // Assert: موجودی باید به ۱۰ برگردد.
        await using (var db = new TestApplicationDbContext(options))
        {
            var sale = await db.Sales
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == saleId);

            Assert.NotNull(sale);
            Assert.Equal(SaleStatus.Cancelled, sale.Status);

            var inventoryAfter = await db.Inventories
                .AsNoTracking()
                .FirstAsync(i =>
                    i.ProductId == productId &&
                    i.WarehouseId == warehouseId);

            Assert.Equal(10, inventoryAfter.Quantity);
        }
    }

    [Fact]
    public async Task CancelSale_Duplicate_ShouldThrowException()
    {
        // Arrange
        var options = CreateOptions();
        var (productId, warehouseId) = await SeedBasicData(options, 10);

        Guid saleId;

        await using (var db = new TestApplicationDbContext(options))
        {
            var service = new SaleService(db);

            saleId = await service.CreateSaleAsync(new CreateSaleDto
            {
                InvoiceNumber = "INV-03",
                CustomerName = "علی رضایی",
                WarehouseId = warehouseId,
                FinalizeImmediately = true,
                Items = new List<CreateSaleItemDto>
                {
                    new()
                    {
                        ProductId = productId,
                        WarehouseId = warehouseId,
                        Quantity = 2,
                        UnitPrice = 300m
                    }
                }
            });
        }

        await using (var db = new TestApplicationDbContext(options))
        {
            var service = new SaleService(db);
            await service.CancelSaleAsync(saleId, "علت اول");
        }

        // Act & Assert
        await using (var db = new TestApplicationDbContext(options))
        {
            var service = new SaleService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelSaleAsync(saleId, "علت دوم"));
        }
    }
}
