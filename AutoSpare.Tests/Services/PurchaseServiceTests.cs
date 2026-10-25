using AutoSpare.Application.Purchases.DTOs;
using AutoSpare.Domain.Brands;
using AutoSpare.Domain.Categories;
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
                x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _service = new PurchaseService(_context);
    }

    private async Task<(Guid SupplierId, Guid WarehouseId, Guid ProductId)> SeedBaseDataAsync()
    {
        var supplier = new Supplier("تجهیزات خودرو ایرانیان", "02122222222", "تهران");
        var warehouse = new Warehouse("انبار مرکزی قطعات", "تهران");
        var category = new Category("قطعات موتوری");
        var brand = new Brand("ایران خودرو");

        await _context.Suppliers.AddAsync(supplier);
        await _context.Warehouses.AddAsync(warehouse);
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
            defaultWarehouseId: warehouse.Id
        );
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        return (supplier.Id, warehouse.Id, product.Id);
    }

    [Fact]
    public async Task CreatePurchase_WithFinalizeImmediately_ShouldIncreaseInventoryAndSucceed()
    {
        // Arrange
        var (supplierId, warehouseId, productId) = await SeedBaseDataAsync();

        var dto = new CreatePurchaseDto
        {
            InvoiceNumber = "PUR-1405-001",
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            FinalizeImmediately = true,
            Items = new List<CreatePurchaseItemDto>
            {
                new() { ProductId = productId, Quantity = 20, UnitPrice = 160_000m }
            }
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
        purchase.Items.Should().HaveCount(1);

        // بررسی افزایش موجودی انبار
        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId);

        inventory.Should().NotBeNull();
        inventory!.Quantity.Should().Be(20);
    }

    [Fact]
    public async Task CreatePurchase_WithDuplicateInvoiceNumber_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var (supplierId, warehouseId, productId) = await SeedBaseDataAsync();

        var existingPurchase = new Purchase("PUR-DUP-001", supplierId, warehouseId);
        existingPurchase.AddOrUpdateItem(productId, 5, 100_000m);
        await _context.Purchases.AddAsync(existingPurchase);
        await _context.SaveChangesAsync();

        var dto = new CreatePurchaseDto
        {
            InvoiceNumber = "PUR-DUP-001",
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            FinalizeImmediately = false,
            Items = new List<CreatePurchaseItemDto>
            {
                new() { ProductId = productId, Quantity = 10, UnitPrice = 120_000m }
            }
        };

        // Act
        var act = async () => await _service.CreatePurchaseAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*قبلاً ثبت شده است*");
    }

    [Fact]
    public async Task CreatePurchase_WithoutSupplier_ShouldThrowArgumentException()
    {
        // Arrange
        var (_, warehouseId, productId) = await SeedBaseDataAsync();

        var dto = new CreatePurchaseDto
        {
            InvoiceNumber = "PUR-002",
            SupplierId = null, // بدون تأمین‌کننده
            WarehouseId = warehouseId,
            Items = new List<CreatePurchaseItemDto>
            {
                new() { ProductId = productId, Quantity = 5, UnitPrice = 100_000m }
            }
        };

        // Act
        var act = async () => await _service.CreatePurchaseAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*تأمین‌کننده*");
    }

    [Fact]
    public async Task FinalizePurchase_ExistingDraft_ShouldSucceedAndIncreaseInventory()
    {
        // Arrange
        var (supplierId, warehouseId, productId) = await SeedBaseDataAsync();

        var purchase = new Purchase("PUR-DRAFT-01", supplierId, warehouseId);
        purchase.AddOrUpdateItem(productId, 10, 150_000m);
        await _context.Purchases.AddAsync(purchase);
        await _context.SaveChangesAsync();

        // Act
        await _service.FinalizePurchaseAsync(purchase.Id);

        // Assert
        var updatedPurchase = await _context.Purchases.FindAsync(purchase.Id);
        updatedPurchase!.Status.Should().Be(PurchaseStatus.Completed);

        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId);

        inventory.Should().NotBeNull();
        inventory!.Quantity.Should().Be(10);
    }

    [Fact]
    public async Task CancelPurchase_Draft_ShouldSucceed()
    {
        // Arrange
        var (supplierId, warehouseId, productId) = await SeedBaseDataAsync();

        var purchase = new Purchase("PUR-CANCEL-01", supplierId, warehouseId);
        purchase.AddOrUpdateItem(productId, 5, 100_000m);
        await _context.Purchases.AddAsync(purchase);
        await _context.SaveChangesAsync();

        // Act
        await _service.CancelPurchaseAsync(purchase.Id);

        // Assert
        var cancelledPurchase = await _context.Purchases.FindAsync(purchase.Id);
        cancelledPurchase!.Status.Should().Be(PurchaseStatus.Cancelled);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
