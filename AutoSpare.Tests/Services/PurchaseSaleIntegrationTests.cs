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

namespace AutoSpare.Tests.Services;

/// <summary>
/// تست جریان خرید، فروش، ابطال و گردش موجودی با دیتابیس InMemory.
/// آزمون رفتار RowVersion واقعی باید جداگانه روی SQL Server اجرا شود.
/// </summary>
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
                    Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId
                        .TransactionIgnoredWarning))
            .Options;
    }

    private ApplicationDbContext CreateContext() =>
        new TestApplicationDbContext(_options);

    private async Task<(Guid SupplierId, Guid WarehouseId, Guid ProductId)>
        SeedMasterDataAsync()
    {
        await using var db = CreateContext();

        var supplier = new Supplier(
            "تأمین‌کننده قطعات یدکی مرکزی",
            "02177889900",
            "تهران، چراغ‌برق");

        var warehouse = new Warehouse(
            "انبار پخش مرکزی",
            "تهران، خیابان امیرکبیر");

        var category = new Category("سیستم ترمز و انتقال قدرت");
        var brand = new Brand("ایساکو");

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
            defaultWarehouseId: warehouse.Id
        );

        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        return (supplier.Id, warehouse.Id, product.Id);
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
            WarehouseId = warehouseId,
            FinalizeImmediately = true,
            Items = new List<CreatePurchaseItemDto>
            {
                new()
                {
                    ProductId = productId,
                    Quantity = 20,
                    UnitPrice = 420_000m
                }
            }
        };

        // گام ۱: ثبت و نهایی‌سازی خرید.
        await using (var db = CreateContext())
        {
            var purchaseService = new PurchaseService(db);

            var purchaseId = await purchaseService.CreatePurchaseAsync(
                purchaseDto,
                "مدیر خرید");

            purchaseId.Should().NotBeEmpty();
        }

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
            Items = new List<CreateSaleItemDto>
            {
                new()
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    Quantity = 7,
                    UnitPrice = 550_000m
                }
            }
        };

        // گام ۲: فروش ۷ عدد.
        Guid saleId;

        await using (var db = CreateContext())
        {
            var saleService = new SaleService(db);
            saleId = await saleService.CreateSaleAsync(saleDto);

            saleId.Should().NotBeEmpty();
        }

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

        // گام ۳: ابطال فروش و بازگشت ۷ عدد.
        await using (var db = CreateContext())
        {
            var saleService = new SaleService(db);
            await saleService.CancelSaleAsync(saleId);
        }

        // Assert نهایی
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
            Items = new List<CreatePurchaseItemDto>
            {
                new()
                {
                    ProductId = productId,
                    Quantity = 10,
                    UnitPrice = 400_000m
                }
            }
        };

        Guid purchaseId;

        await using (var db = CreateContext())
        {
            var purchaseService = new PurchaseService(db);
            purchaseId = await purchaseService.CreatePurchaseAsync(purchaseDto);
        }

        await using (var db = CreateContext())
        {
            var purchaseService = new PurchaseService(db);
            await purchaseService.CancelPurchaseAsync(purchaseId);
        }

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
            Items = new List<CreateSaleItemDto>
            {
                new()
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    Quantity = 1,
                    UnitPrice = 500_000m
                }
            }
        };

        // فروش نباید بتواند روی موجودیِ حاصل‌نشده از خرید پیش‌نویس انجام شود.
        await using (var db = CreateContext())
        {
            var saleService = new SaleService(db);

            var act = async () =>
                await saleService.CreateSaleAsync(saleDto);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}
