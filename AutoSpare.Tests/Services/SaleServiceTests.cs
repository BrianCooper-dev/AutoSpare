using AutoSpare.Application.Sales.DTOs;
using AutoSpare.Domain.Brands;
using AutoSpare.Domain.Categories;
using AutoSpare.Domain.Inventories;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Sales.Enums;
using AutoSpare.Domain.Warehouses;
using AutoSpare.Infrastructure.Persistence;
using AutoSpare.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoSpare.Tests.Services;

public class SaleServiceTests
{
    private static DbContextOptions<ApplicationDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static async Task<(Guid ProductId, Guid WarehouseId)> SeedBasicData(
        DbContextOptions<ApplicationDbContext> options, int initialStock)
    {
        await using var db = new ApplicationDbContext(options);

        // ۱. ایجاد انبار
        var warehouse = new Warehouse("انبار مرکزی", "تهران", "02100000000");
        await db.Warehouses.AddAsync(warehouse);

        // ۲. ایجاد دسته‌بندی و برند
        var category = new Category("لوازم یدکی", "Spare Parts");
        var brand = new Brand("ایران خودرو", "IKCO");
        await db.Categories.AddAsync(category);
        await db.Brands.AddAsync(brand);

        // ۳. ایجاد کالا
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

        // ۴. ایجاد موجودی اولیه انبار
        var inventory = new Inventory(product.Id, warehouse.Id, initialStock);
        await db.Inventories.AddAsync(inventory);

        await db.SaveChangesAsync();

        return (product.Id, warehouse.Id);
    }

    [Fact]
    public async Task CreateSale_WithFinalize_ShouldDecreaseInventory()
    {
        // Arrange
        var options = CreateOptions();
        var (pId, wId) = await SeedBasicData(options, 10);

        var dto = new CreateSaleDto
        {
            InvoiceNumber = "INV-01",
            CustomerName = "علی رضایی",
            WarehouseId = wId,
            FinalizeImmediately = true,
            Items = new List<CreateSaleItemDto>
            {
                new() { ProductId = pId, WarehouseId = wId, Quantity = 3, UnitPrice = 300m }
            }
        };

        // Act
        Guid saleId;
        await using (var db = new ApplicationDbContext(options))
        {
            var service = new SaleService(db);
            saleId = await service.CreateSaleAsync(dto);
        }

        // Assert
        await using (var db = new ApplicationDbContext(options))
        {
            var sale = await db.Sales.FirstOrDefaultAsync(s => s.Id == saleId);
            Assert.NotNull(sale);
            Assert.Equal(SaleStatus.Completed, sale.Status);

            var inventory = await db.Inventories.FirstAsync(i => i.ProductId == pId && i.WarehouseId == wId);
            Assert.Equal(7, inventory.Quantity); // ۱۰ - ۳ = ۷
        }
    }

    [Fact]
    public async Task CancelSale_ShouldRestoreInventory()
    {
        // Arrange
        var options = CreateOptions();
        var (pId, wId) = await SeedBasicData(options, 10);
        Guid saleId;

        await using (var db = new ApplicationDbContext(options))
        {
            var service = new SaleService(db);
            saleId = await service.CreateSaleAsync(new CreateSaleDto
            {
                InvoiceNumber = "INV-02",
                CustomerName = "علی رضایی",
                WarehouseId = wId,
                FinalizeImmediately = true,
                Items = new List<CreateSaleItemDto>
                {
                    new() { ProductId = pId, WarehouseId = wId, Quantity = 4, UnitPrice = 300m }
                }
            });
        }

        // بررسی موجودی قبل از ابطال (۱۰ - ۴ = ۶)
        await using (var db = new ApplicationDbContext(options))
        {
            var invBefore = await db.Inventories.FirstAsync(i => i.ProductId == pId && i.WarehouseId == wId);
            Assert.Equal(6, invBefore.Quantity);
        }

        // Act: ابطال فاکتور
        await using (var db = new ApplicationDbContext(options))
        {
            var service = new SaleService(db);
            await service.CancelSaleAsync(saleId, "انصراف مشتری");
        }

        // Assert: بازگشت موجودی به ۱۰
        await using (var db = new ApplicationDbContext(options))
        {
            var sale = await db.Sales.FirstOrDefaultAsync(s => s.Id == saleId);
            Assert.NotNull(sale);
            Assert.Equal(SaleStatus.Cancelled, sale.Status);

            var invAfter = await db.Inventories.FirstAsync(i => i.ProductId == pId && i.WarehouseId == wId);
            Assert.Equal(10, invAfter.Quantity);
        }
    }

    [Fact]
    public async Task CancelSale_Duplicate_ShouldThrowException()
    {
        // Arrange
        var options = CreateOptions();
        var (pId, wId) = await SeedBasicData(options, 10);
        Guid saleId;

        await using (var db = new ApplicationDbContext(options))
        {
            var service = new SaleService(db);
            saleId = await service.CreateSaleAsync(new CreateSaleDto
            {
                InvoiceNumber = "INV-03",
                CustomerName = "علی رضایی",
                WarehouseId = wId,
                FinalizeImmediately = true,
                Items = new List<CreateSaleItemDto>
                {
                    new() { ProductId = pId, WarehouseId = wId, Quantity = 2, UnitPrice = 300m }
                }
            });
            await service.CancelSaleAsync(saleId, "علت اول");
        }

        // Act & Assert: ابطال مجدد باید InvalidOperationException پرتاب کند
        await using (var db = new ApplicationDbContext(options))
        {
            var service = new SaleService(db);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CancelSaleAsync(saleId, "علت دوم")
            );
        }
    }
}
