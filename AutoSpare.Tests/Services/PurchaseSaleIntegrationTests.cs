using AutoSpare.Application.Purchases.DTOs;
using AutoSpare.Application.Sales.DTOs;
using AutoSpare.Domain.Brands;
using AutoSpare.Domain.Categories;
using AutoSpare.Domain.Inventories.Enums;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Purchases.Enums;
using AutoSpare.Domain.Suppliers;
using AutoSpare.Domain.Warehouses;
using AutoSpare.Infrastructure.Persistence;
using AutoSpare.Infrastructure.Services;
using AutoSpare.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AutoSpare.Tests.Services;

public class PurchaseSaleIntegrationTests
{
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public PurchaseSaleIntegrationTests()
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(
                databaseName: $"IntegrationDb_{Guid.NewGuid()}")
            .ConfigureWarnings(x =>
                x.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics
                        .InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private ApplicationDbContext CreateContext() =>
        new TestApplicationDbContext(_options);

    private async Task<(
        Guid SupplierId,
        Guid WarehouseId,
        Guid ProductId)> SeedMasterDataAsync()
    {
        await using var db = CreateContext();

        var supplier = new Supplier(
            "تأمین‌کننده قطعات یدکی مرکزی",
            "02177889900",
            "تهران، چراغ‌برق");

        var warehouse = new Warehouse(
            "انبار پخش مرکزی",
            "تهران، خیابان امیرکبیر",
            "02100000000");

        var category = new Category(
            "سیستم ترمز و انتقال قدرت",
            "Brake System");

        var brand = new Brand(
            "ایساکو",
            "ISACCO");

        await db.Suppliers.AddAsync(supplier);
        await db.Warehouses.AddAsync(warehouse);
        await db.Categories.AddAsync(category);
        await db.Brands.AddAsync(brand);
        await db.SaveChangesAsync();

        var product = new Product(
            name: "دیسک ترمز چرخ جلو",
            internalCode: "DSK-FRN-206",
            model: "پژو ۲۰۶ تیپ ۵",
            purchasePrice: 400_000m,
            salePrice: 550_000m,
            categoryId: category.Id,
            brandId: brand.Id,
            imagePath: null,
            defaultWarehouseId: warehouse.Id);

        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        return (
            supplier.Id,
            warehouse.Id,
            product.Id);
    }

    [Fact]
    public async Task CompleteLifeCycle_PurchaseThenSaleThenCancelSale_InventoryAndMovementsMustMatch()
    {
        // Arrange
        var (supplierId, warehouseId, productId) =
            await SeedMasterDataAsync();

        var purchaseDto = new CreatePurchaseDto
        {
            InvoiceNumber = "INT-PUR-001",
            SupplierId = supplierId,

            // این مقدار صرفاً جهت سازگاری DTO نگه داشته شده است.
            // منبع اصلی انبار، WarehouseId هر ردیف است.
            WarehouseId = warehouseId,

            FinalizeImmediately = true,
            Items =
            [
                new CreatePurchaseItemDto
                {
                    ProductId = productId,

                    // در ساختار جدید، انبار هر ردیف الزامی است.
                    WarehouseId = warehouseId,

                    Quantity = 20,
                    UnitPrice = 420_000m
                }
            ]
        };

        var purchaseLoggerMock =
            new Mock<ILogger<PurchaseService>>();

        var saleLoggerMock =
            new Mock<ILogger<SaleService>>();

        // Act: ثبت و نهایی‌سازی خرید
        await using (var db = CreateContext())
        {
            var purchaseService = new PurchaseService(
                db,
                purchaseLoggerMock.Object);

            var purchaseId =
                await purchaseService.CreatePurchaseAsync(
                    purchaseDto,
                    "مدیر خرید");

            purchaseId.Should().NotBeEmpty();
        }

        // Assert: موجودی پس از خرید
        await using (var db = CreateContext())
        {
            var inventoryAfterPurchase = await db.Inventories
                .AsNoTracking()
                .FirstOrDefaultAsync(i =>
                    i.ProductId == productId &&
                    i.WarehouseId == warehouseId);

            inventoryAfterPurchase.Should().NotBeNull();
            inventoryAfterPurchase!.Quantity.Should().Be(20);
        }

        var saleDto = new CreateSaleDto
        {
            InvoiceNumber = "INT-SALE-001",
            CustomerName = "تعمیرگاه تخصصی کاردان",
            WarehouseId = warehouseId,
            FinalizeImmediately = true,
            Items =
            [
                new CreateSaleItemDto
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    Quantity = 7,
                    UnitPrice = 550_000m
                }
            ]
        };

        Guid saleId;

        // Act: ثبت فروش
        await using (var db = CreateContext())
        {
            var saleService = new SaleService(
                db,
                saleLoggerMock.Object);

            saleId = await saleService.CreateSaleAsync(saleDto);

            saleId.Should().NotBeEmpty();
        }

        // Assert: موجودی پس از فروش
        await using (var db = CreateContext())
        {
            var inventoryAfterSale = await db.Inventories
                .AsNoTracking()
                .FirstOrDefaultAsync(i =>
                    i.ProductId == productId &&
                    i.WarehouseId == warehouseId);

            inventoryAfterSale.Should().NotBeNull();
            inventoryAfterSale!.Quantity.Should().Be(13);
        }

        // Act: ابطال فروش
        await using (var db = CreateContext())
        {
            var saleService = new SaleService(
                db,
                saleLoggerMock.Object);

            await saleService.CancelSaleAsync(saleId);
        }

        // Assert: موجودی و گردش‌های نهایی
        await using (var db = CreateContext())
        {
            var finalInventory = await db.Inventories
                .AsNoTracking()
                .Include(i => i.Movements)
                .FirstOrDefaultAsync(i =>
                    i.ProductId == productId &&
                    i.WarehouseId == warehouseId);

            finalInventory.Should().NotBeNull();
            finalInventory!.Quantity.Should().Be(20);

            var movements = finalInventory.Movements.ToList();

            movements.Should().HaveCount(3);

            var purchaseMovement = movements.Single(m => m.Type == StockMovementType.Purchase);

            purchaseMovement.Reference.Should().Be("INT-PUR-001");
            purchaseMovement.QuantityChange.Should().Be(20);
            purchaseMovement.BalanceAfter.Should().Be(20);

            var saleMovement = movements.Single(m => m.Type == StockMovementType.Sale);

            saleMovement.Reference.Should().Be("INT-SALE-001");
            saleMovement.QuantityChange.Should().Be(-7);
            saleMovement.BalanceAfter.Should().Be(13);

            var returnMovement = movements.Single(m => m.Type == StockMovementType.CustomerReturn);

            returnMovement.Reference.Should().Be("INT-SALE-001");
            returnMovement.QuantityChange.Should().Be(7);
            returnMovement.BalanceAfter.Should().Be(20);
        }
    }

    [Fact]
    public async Task PurchaseDraft_Cancelled_CannotBeSoldDueToInsufficientStock()
    {
        // Arrange
        var (supplierId, warehouseId, productId) =
            await SeedMasterDataAsync();

        var purchaseDto = new CreatePurchaseDto
        {
            InvoiceNumber = "INT-PUR-DRAFT-01",
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            FinalizeImmediately = false,
            Items =
            [
                new CreatePurchaseItemDto
                {
                    ProductId = productId,

                    // در ساختار جدید، انبار ردیف باید مشخص باشد.
                    WarehouseId = warehouseId,

                    Quantity = 10,
                    UnitPrice = 400_000m
                }
            ]
        };

        Guid purchaseId;

        var purchaseLoggerMock =
            new Mock<ILogger<PurchaseService>>();

        var saleLoggerMock =
            new Mock<ILogger<SaleService>>();

        // Act: ایجاد فاکتور پیش‌نویس
        await using (var db = CreateContext())
        {
            var purchaseService = new PurchaseService(
                db,
                purchaseLoggerMock.Object);

            purchaseId = await purchaseService.CreatePurchaseAsync(
                purchaseDto);
        }

        // Act: ابطال فاکتور پیش‌نویس
        await using (var db = CreateContext())
        {
            var purchaseService = new PurchaseService(
                db,
                purchaseLoggerMock.Object);

            await purchaseService.CancelPurchaseAsync(purchaseId);
        }

        // Assert: وضعیت خرید
        await using (var db = CreateContext())
        {
            var purchase = await db.Purchases
                .AsNoTracking()
                .FirstAsync(p => p.Id == purchaseId);

            purchase.Status.Should().Be(PurchaseStatus.Cancelled);
        }

        var saleDto = new CreateSaleDto
        {
            InvoiceNumber = "INT-SALE-FAIL-01",
            CustomerName = "مشتری متفرقه",
            WarehouseId = warehouseId,
            FinalizeImmediately = true,
            Items =
            [
                new CreateSaleItemDto
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    Quantity = 1,
                    UnitPrice = 500_000m
                }
            ]
        };

        // Act & Assert:
        // چون خرید پیش‌نویس بوده، موجودی افزایش پیدا نکرده است.
        await using (var db = CreateContext())
        {
            var saleService = new SaleService(
                db,
                saleLoggerMock.Object);

            var act = async () =>
                await saleService.CreateSaleAsync(saleDto);

            await act.Should()
                .ThrowAsync<InvalidOperationException>();
        }
    }
}
