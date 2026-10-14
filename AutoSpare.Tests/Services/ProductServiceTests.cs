using AutoSpare.Application.Products.DTOs;
using AutoSpare.Domain.Inventories;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Warehouses;
using AutoSpare.Infrastructure.Persistence;
using AutoSpare.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Tests.Services;

public class ProductServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _service = new ProductService(_context);
    }

    [Fact]
    public async Task CreateProduct_WithUniqueInternalCode_ShouldSucceed()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var brandId = Guid.NewGuid();

        var dto = new CreateProductDto
        {
            Name = "فیلتر روغن پژو ۲۰۶",
            InternalCode = "FL-206-01",
            Model = "TU5",
            PurchasePrice = 100_000,
            SalePrice = 140_000,
            CategoryId = categoryId,
            BrandId = brandId
        };

        // Act
        var productId = await _service.CreateProductAsync(dto);

        // Assert
        productId.Should().NotBeEmpty();
        var product = await _context.Products.FindAsync(productId);
        product.Should().NotBeNull();
        product!.InternalCode.Should().Be("FL-206-01");
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateInternalCode_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var existingProduct = new Product(
            name: "شمع سوزنی ایریدیوم",
            internalCode: "SP-001",
            model: "انواع خودرو",
            purchasePrice: 200_000,
            salePrice: 260_000,
            categoryId: Guid.NewGuid(),
            brandId: Guid.NewGuid(),
            imagePath: null,
            defaultWarehouseId: null
        );
        await _context.Products.AddAsync(existingProduct);
        await _context.SaveChangesAsync();

        var duplicateDto = new CreateProductDto
        {
            Name = "شمع موتور دبل پلاتینیوم",
            InternalCode = "SP-001",
            Model = "پژو پارس",
            PurchasePrice = 250_000,
            SalePrice = 300_000,
            CategoryId = Guid.NewGuid(),
            BrandId = Guid.NewGuid()
        };

        // Act
        var act = async () => await _service.CreateProductAsync(duplicateDto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*قبلاً در سیستم ثبت شده است*");
    }

    [Fact]
    public async Task CreateProduct_WithInitialStockAndWarehouse_ShouldCreateInventoryRecord()
    {
        // Arrange
        var warehouse = new Warehouse("انبار مرکزی", "تهران");
        await _context.Warehouses.AddAsync(warehouse);
        await _context.SaveChangesAsync();

        var dto = new CreateProductDto
        {
            Name = "تسمه تایم کنتیننتال",
            InternalCode = "TB-CONT-101",
            Model = "سمند EF7",
            PurchasePrice = 500_000,
            SalePrice = 650_000,
            CategoryId = Guid.NewGuid(),
            BrandId = Guid.NewGuid(),
            DefaultWarehouseId = warehouse.Id,
            InitialQuantity = 15
        };

        // Act
        var productId = await _service.CreateProductAsync(dto);

        // Assert
        productId.Should().NotBeEmpty();
        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouse.Id);

        inventory.Should().NotBeNull();
        inventory!.Quantity.Should().Be(15);
    }

    [Fact]
    public async Task CreateProduct_WithInitialStock_WithoutWarehouse_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var dto = new CreateProductDto
        {
            Name = "دیسک و صفحه والئو",
            InternalCode = "VAL-CLUTCH-01",
            Model = "پراید",
            PurchasePrice = 3_000_000,
            SalePrice = 3_600_000,
            CategoryId = Guid.NewGuid(),
            BrandId = Guid.NewGuid(),
            DefaultWarehouseId = null,
            InitialQuantity = 5
        };

        // Act
        var act = async () => await _service.CreateProductAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*انتخاب انبار الزامی است*");
    }

    [Fact]
    public async Task GetProductsAsync_WhenStockIsLessThanOrEqualToThreshold_ShouldIdentifyAsLowStock()
    {
        // Arrange
        var warehouse = new Warehouse("انبار قطعات", "تبریز");
        await _context.Warehouses.AddAsync(warehouse);

        var product = new Product(
            name: "لنت ترمز جلو",
            internalCode: "BP-001",
            model: "پژو ۴۰۵",
            purchasePrice: 400_000,
            salePrice: 550_000,
            categoryId: Guid.NewGuid(),
            brandId: Guid.NewGuid(),
            defaultWarehouseId: warehouse.Id,
            isLowStockAlertEnabled: true,
            minStockThreshold: 10
        );
        await _context.Products.AddAsync(product);

        var inventory = new Inventory(product.Id, warehouse.Id, initialQuantity: 8);
        await _context.Inventories.AddAsync(inventory);
        await _context.SaveChangesAsync();

        // Act
        var products = await _service.GetProductsAsync(new ProductFilterDto());

        // Assert
        products.Should().ContainSingle();
        var item = products.First();
        item.TotalStock.Should().Be(8);
        item.MinStockThreshold.Should().Be(10);
        item.IsLowStockAlertEnabled.Should().BeTrue();
        item.IsLowStock.Should().BeTrue();
    }

    [Fact]
    public async Task GetProductsAsync_WithLowStockFilter_ShouldReturnOnlyLowStockProducts()
    {
        // Arrange
        var warehouse = new Warehouse("انبار اصلی", "تهران");
        await _context.Warehouses.AddAsync(warehouse);

        // کالای دارای کمبود موجودی (آستانه ۱۰، موجودی ۶)
        var lowStockProduct = new Product(
            name: "شمع پایه کوتاه",
            internalCode: "SP-LOW",
            model: "۲۰۶",
            purchasePrice: 80_000,
            salePrice: 120_000,
            categoryId: Guid.NewGuid(),
            brandId: Guid.NewGuid(),
            defaultWarehouseId: warehouse.Id,
            isLowStockAlertEnabled: true,
            minStockThreshold: 10
        );

        // کالای با موجودی کافی (آستانه ۵، موجودی ۲۰)
        var normalStockProduct = new Product(
            name: "وایر شمع",
            internalCode: "WR-NORM",
            model: "۲۰۶",
            purchasePrice: 150_000,
            salePrice: 200_000,
            categoryId: Guid.NewGuid(),
            brandId: Guid.NewGuid(),
            defaultWarehouseId: warehouse.Id,
            isLowStockAlertEnabled: true,
            minStockThreshold: 5
        );

        await _context.Products.AddRangeAsync(lowStockProduct, normalStockProduct);
        await _context.Inventories.AddRangeAsync(
            new Inventory(lowStockProduct.Id, warehouse.Id, initialQuantity: 6),
            new Inventory(normalStockProduct.Id, warehouse.Id, initialQuantity: 20)
        );
        await _context.SaveChangesAsync();

        var filter = new ProductFilterDto
        {
            StockStatus = StockStatusFilter.LowStock
        };

        // Act
        var result = await _service.GetProductsAsync(filter);

        // Assert
        result.Should().ContainSingle();
        result.First().InternalCode.Should().Be("SP-LOW");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
