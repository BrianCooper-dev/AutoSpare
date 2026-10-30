using AutoSpare.Application.Purchases.DTOs;
using AutoSpare.Domain.Brands;
using AutoSpare.Domain.Categories;
using AutoSpare.Domain.Inventories.Enums;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Purchases;
using AutoSpare.Domain.Purchases.Enums;
using AutoSpare.Domain.Suppliers;
using AutoSpare.Domain.Warehouses;
using AutoSpare.Infrastructure.Persistence;
using AutoSpare.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Tests.Services;

public class PurchaseServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PurchaseService _service;

    public PurchaseServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x =>
                x.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics
                        .InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _service = new PurchaseService(_context);
    }

    private async Task<(
        Guid SupplierId,
        Guid WarehouseId,
        Guid SecondWarehouseId,
        Guid ProductId)> SeedBaseDataAsync()
    {
        var supplier = new Supplier(
            "تجهیزات خودرو ایرانیان",
            "02122222222",
            "تهران");

        var warehouse = new Warehouse(
            "انبار مرکزی قطعات",
            "تهران");

        var secondWarehouse = new Warehouse(
            "انبار شماره دو",
            "کرج");

        var category = new Category("قطعات موتوری");
        var brand = new Brand("ایران خودرو");

        await _context.Suppliers.AddAsync(supplier);
        await _context.Warehouses.AddRangeAsync(warehouse, secondWarehouse);
        await _context.Categories.AddAsync(category);
        await _context.Brands.AddAsync(brand);
        await _context.SaveChangesAsync();

        var product = new Product(
            name: "واشر سر سیلندر",
            internalCode: "GSK-001",
            model: "EF7",
            purchasePrice: 150_000m,
            salePrice: 200_000m,
            categoryId: category.Id,
            brandId: brand.Id,
            defaultWarehouseId: warehouse.Id);

        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        return (
            supplier.Id,
            warehouse.Id,
            secondWarehouse.Id,
            product.Id);
    }

    [Fact]
    public async Task CreatePurchase_WithFinalizeImmediately_ShouldIncreaseSelectedRowInventory()
    {
        // Arrange
        var (supplierId, warehouseId, _, productId) =
            await SeedBaseDataAsync();

        var dto = new CreatePurchaseDto
        {
            InvoiceNumber = "PUR-1405-001",
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            FinalizeImmediately = true,
            Items =
            [
                new CreatePurchaseItemDto
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    Quantity = 20,
                    UnitPrice = 160_000m
                }
            ]
        };

        // Act
        var purchaseId = await _service.CreatePurchaseAsync(dto);

        // Assert
        purchaseId.Should().NotBeEmpty();

        var purchase = await _context.Purchases
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == purchaseId);

        purchase.Should().NotBeNull();
        purchase!.Status.Should().Be(PurchaseStatus.Completed);
        purchase.Items.Should().ContainSingle();

        var purchaseItem = purchase.Items.Single();

        purchaseItem.ProductId.Should().Be(productId);
        purchaseItem.WarehouseId.Should().Be(warehouseId);
        purchaseItem.Quantity.Should().Be(20);

        var inventory = await _context.Inventories
            .Include(i => i.Movements)
            .FirstOrDefaultAsync(i =>
                i.ProductId == productId &&
                i.WarehouseId == warehouseId);

        inventory.Should().NotBeNull();
        inventory!.Quantity.Should().Be(20);

        inventory.Movements.Should().ContainSingle(m =>
            m.Type == StockMovementType.Purchase &&
            m.QuantityChange == 20);
    }

    [Fact]
    public async Task CreatePurchase_WithTwoWarehousesForSameProduct_ShouldCreateTwoItemsAndIncreaseBothInventories()
    {
        // Arrange
        var (
            supplierId,
            warehouseId,
            secondWarehouseId,
            productId) = await SeedBaseDataAsync();

        var dto = new CreatePurchaseDto
        {
            InvoiceNumber = "PUR-MULTI-WH-001",
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            FinalizeImmediately = true,
            Items =
            [
                new CreatePurchaseItemDto
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    Quantity = 7,
                    UnitPrice = 160_000m
                },
                new CreatePurchaseItemDto
                {
                    ProductId = productId,
                    WarehouseId = secondWarehouseId,
                    Quantity = 13,
                    UnitPrice = 165_000m
                }
            ]
        };

        // Act
        var purchaseId = await _service.CreatePurchaseAsync(dto);

        // Assert
        var purchase = await _context.Purchases
            .Include(p => p.Items)
            .SingleAsync(p => p.Id == purchaseId);

        purchase.Status.Should().Be(PurchaseStatus.Completed);
        purchase.Items.Should().HaveCount(2);

        purchase.Items.Should().ContainSingle(i =>
            i.ProductId == productId &&
            i.WarehouseId == warehouseId &&
            i.Quantity == 7);

        purchase.Items.Should().ContainSingle(i =>
            i.ProductId == productId &&
            i.WarehouseId == secondWarehouseId &&
            i.Quantity == 13);

        var firstInventory = await _context.Inventories
            .SingleAsync(i =>
                i.ProductId == productId &&
                i.WarehouseId == warehouseId);

        var secondInventory = await _context.Inventories
            .SingleAsync(i =>
                i.ProductId == productId &&
                i.WarehouseId == secondWarehouseId);

        firstInventory.Quantity.Should().Be(7);
        secondInventory.Quantity.Should().Be(13);
    }

    [Fact]
    public async Task CreatePurchase_WithDuplicateProductAndWarehouse_ShouldMergeRows()
    {
        // Arrange
        var (supplierId, warehouseId, _, productId) =
            await SeedBaseDataAsync();

        var dto = new CreatePurchaseDto
        {
            InvoiceNumber = "PUR-MERGE-001",
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            FinalizeImmediately = true,
            Items =
            [
                new CreatePurchaseItemDto
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    Quantity = 4,
                    UnitPrice = 150_000m
                },
                new CreatePurchaseItemDto
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    Quantity = 6,
                    UnitPrice = 170_000m
                }
            ]
        };

        // Act
        var purchaseId = await _service.CreatePurchaseAsync(dto);

        // Assert
        var purchase = await _context.Purchases
            .Include(p => p.Items)
            .SingleAsync(p => p.Id == purchaseId);

        purchase.Items.Should().ContainSingle();

        var item = purchase.Items.Single();

        item.Quantity.Should().Be(10);
        item.UnitPrice.Should().Be(170_000m);
        item.WarehouseId.Should().Be(warehouseId);

        var inventory = await _context.Inventories
            .SingleAsync(i =>
                i.ProductId == productId &&
                i.WarehouseId == warehouseId);

        inventory.Quantity.Should().Be(10);
    }

    [Fact]
    public async Task CreatePurchase_WithoutItemWarehouse_ShouldUseHeaderWarehouse()
    {
        // Arrange
        var (supplierId, warehouseId, _, productId) =
            await SeedBaseDataAsync();

        var dto = new CreatePurchaseDto
        {
            InvoiceNumber = "PUR-FALLBACK-001",
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            FinalizeImmediately = true,
            Items =
            [
                new CreatePurchaseItemDto
                {
                    ProductId = productId,
                    WarehouseId = null,
                    Quantity = 8,
                    UnitPrice = 155_000m
                }
            ]
        };

        // Act
        var purchaseId = await _service.CreatePurchaseAsync(dto);

        // Assert
        var purchase = await _context.Purchases
            .Include(p => p.Items)
            .SingleAsync(p => p.Id == purchaseId);

        purchase.Items.Should().ContainSingle();
        purchase.Items.Single().WarehouseId.Should().Be(warehouseId);

        var inventory = await _context.Inventories
            .SingleAsync(i =>
                i.ProductId == productId &&
                i.WarehouseId == warehouseId);

        inventory.Quantity.Should().Be(8);
    }

    [Fact]
    public async Task CreatePurchase_AsDraft_ShouldNotIncreaseInventory()
    {
        // Arrange
        var (
            supplierId,
            warehouseId,
            secondWarehouseId,
            productId) = await SeedBaseDataAsync();

        var dto = new CreatePurchaseDto
        {
            InvoiceNumber = "PUR-DRAFT-CREATE-001",
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            FinalizeImmediately = false,
            Items =
            [
                new CreatePurchaseItemDto
                {
                    ProductId = productId,
                    WarehouseId = secondWarehouseId,
                    Quantity = 9,
                    UnitPrice = 160_000m
                }
            ]
        };

        // Act
        var purchaseId = await _service.CreatePurchaseAsync(dto);

        // Assert
        var purchase = await _context.Purchases
            .Include(p => p.Items)
            .SingleAsync(p => p.Id == purchaseId);

        purchase.Status.Should().Be(PurchaseStatus.Draft);
        purchase.Items.Single().WarehouseId.Should().Be(secondWarehouseId);

        var inventoryExists = await _context.Inventories.AnyAsync(i =>
            i.ProductId == productId &&
            i.WarehouseId == secondWarehouseId);

        inventoryExists.Should().BeFalse();
    }

    [Fact]
    public async Task CreatePurchase_WithDuplicateInvoiceNumber_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var (supplierId, warehouseId, _, productId) =
            await SeedBaseDataAsync();

        var existingPurchase = new Purchase(
            "PUR-DUP-001",
            supplierId,
            warehouseId);

        existingPurchase.AddOrUpdateItem(
            productId,
            warehouseId,
            5,
            100_000m);

        await _context.Purchases.AddAsync(existingPurchase);
        await _context.SaveChangesAsync();

        var dto = new CreatePurchaseDto
        {
            InvoiceNumber = "PUR-DUP-001",
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            FinalizeImmediately = false,
            Items =
            [
                new CreatePurchaseItemDto
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    Quantity = 10,
                    UnitPrice = 120_000m
                }
            ]
        };

        // Act
        var act = async () => await _service.CreatePurchaseAsync(dto);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*قبلاً ثبت شده است*");
    }

    [Fact]
    public async Task CreatePurchase_WithoutSupplier_ShouldThrowArgumentException()
    {
        // Arrange
        var (_, warehouseId, _, productId) =
            await SeedBaseDataAsync();

        var dto = new CreatePurchaseDto
        {
            InvoiceNumber = "PUR-002",
            SupplierId = null,
            WarehouseId = warehouseId,
            Items =
            [
                new CreatePurchaseItemDto
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    Quantity = 5,
                    UnitPrice = 100_000m
                }
            ]
        };

        // Act
        var act = async () => await _service.CreatePurchaseAsync(dto);

        // Assert
        await act.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*تأمین‌کننده*");
    }

    [Fact]
    public async Task FinalizePurchase_ExistingDraft_ShouldIncreaseInventoryOfEachItemWarehouse()
    {
        // Arrange
        var (
            supplierId,
            warehouseId,
            secondWarehouseId,
            productId) = await SeedBaseDataAsync();

        var purchase = new Purchase(
            "PUR-DRAFT-01",
            supplierId,
            warehouseId);

        purchase.AddOrUpdateItem(
            productId,
            secondWarehouseId,
            10,
            150_000m);

        await _context.Purchases.AddAsync(purchase);
        await _context.SaveChangesAsync();

        // Act
        await _service.FinalizePurchaseAsync(purchase.Id);

        // Assert
        var updatedPurchase = await _context.Purchases
            .Include(p => p.Items)
            .SingleAsync(p => p.Id == purchase.Id);

        updatedPurchase.Status.Should().Be(PurchaseStatus.Completed);
        updatedPurchase.Items.Single().WarehouseId
            .Should().Be(secondWarehouseId);

        var targetInventory = await _context.Inventories
            .FirstOrDefaultAsync(i =>
                i.ProductId == productId &&
                i.WarehouseId == secondWarehouseId);

        targetInventory.Should().NotBeNull();
        targetInventory!.Quantity.Should().Be(10);

        var headerWarehouseInventoryExists =
            await _context.Inventories.AnyAsync(i =>
                i.ProductId == productId &&
                i.WarehouseId == warehouseId);

        headerWarehouseInventoryExists.Should().BeFalse();
    }

    [Fact]
    public async Task CancelPurchase_Draft_ShouldSucceedWithoutChangingInventory()
    {
        // Arrange
        var (supplierId, warehouseId, _, productId) =
            await SeedBaseDataAsync();

        var purchase = new Purchase(
            "PUR-CANCEL-01",
            supplierId,
            warehouseId);

        purchase.AddOrUpdateItem(
            productId,
            warehouseId,
            5,
            100_000m);

        await _context.Purchases.AddAsync(purchase);
        await _context.SaveChangesAsync();

        // Act
        await _service.CancelPurchaseAsync(purchase.Id);

        // Assert
        var cancelledPurchase = await _context.Purchases
            .FindAsync(purchase.Id);

        cancelledPurchase!.Status.Should().Be(PurchaseStatus.Cancelled);

        var inventoryExists = await _context.Inventories.AnyAsync(i =>
            i.ProductId == productId &&
            i.WarehouseId == warehouseId);

        inventoryExists.Should().BeFalse();
    }

    [Fact]
    public async Task CancelPurchase_Completed_ShouldDecreaseInventoryOfEachItemWarehouse()
    {
        // Arrange
        var (
            supplierId,
            warehouseId,
            secondWarehouseId,
            productId) = await SeedBaseDataAsync();

        var dto = new CreatePurchaseDto
        {
            InvoiceNumber = "PUR-CANCEL-COMPLETED-001",
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            FinalizeImmediately = true,
            Items =
            [
                new CreatePurchaseItemDto
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    Quantity = 5,
                    UnitPrice = 150_000m
                },
                new CreatePurchaseItemDto
                {
                    ProductId = productId,
                    WarehouseId = secondWarehouseId,
                    Quantity = 7,
                    UnitPrice = 155_000m
                }
            ]
        };

        var purchaseId = await _service.CreatePurchaseAsync(dto);

        // Act
        await _service.CancelPurchaseAsync(
            purchaseId,
            "اشتباه در ثبت فاکتور",
            "کاربر تست");

        // Assert
        var purchase = await _context.Purchases
            .SingleAsync(p => p.Id == purchaseId);

        purchase.Status.Should().Be(PurchaseStatus.Cancelled);

        var firstInventory = await _context.Inventories
            .Include(i => i.Movements)
            .SingleAsync(i =>
                i.ProductId == productId &&
                i.WarehouseId == warehouseId);

        var secondInventory = await _context.Inventories
            .Include(i => i.Movements)
            .SingleAsync(i =>
                i.ProductId == productId &&
                i.WarehouseId == secondWarehouseId);

        firstInventory.Quantity.Should().Be(0);
        secondInventory.Quantity.Should().Be(0);

        firstInventory.Movements.Should().Contain(m =>
            m.Type == StockMovementType.SupplierReturn &&
            m.QuantityChange == -5);

        secondInventory.Movements.Should().Contain(m =>
            m.Type == StockMovementType.SupplierReturn &&
            m.QuantityChange == -7);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
