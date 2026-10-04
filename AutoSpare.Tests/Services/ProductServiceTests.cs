using AutoSpare.Application.Products.DTOs;
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
            InternalCode = "SP-001", // کد تکراری
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
            DefaultWarehouseId = null, // انبار انتخاب نشده
            InitialQuantity = 5 // ولی موجودی ثبت شده
        };

        // Act
        var act = async () => await _service.CreateProductAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*انتخاب انبار الزامی است*");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
