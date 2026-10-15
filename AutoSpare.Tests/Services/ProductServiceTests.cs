using AutoSpare.Application.Products.DTOs;
using AutoSpare.Domain.Brands;
using AutoSpare.Domain.Categories;
using AutoSpare.Domain.Inventories;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Products.Enums;
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
        var category = new Category("سیستم ترمز");
        var brand = new Brand("ایران لنت");
        var warehouse = new Warehouse("انبار قطعات", "تبریز");
        await _context.Categories.AddAsync(category);
        await _context.Brands.AddAsync(brand);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.SaveChangesAsync();

        var product = new Product(
            name: "لنت ترمز جلو",
            internalCode: "BP-001",
            model: "پژو ۴۰۵",
            purchasePrice: 400_000,
            salePrice: 550_000,
            categoryId: category.Id,
            brandId: brand.Id,
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
        var category = new Category("قطعات برقی");
        var brand = new Brand("بوش");
        var warehouse = new Warehouse("انبار اصلی", "تهران");
        await _context.Categories.AddAsync(category);
        await _context.Brands.AddAsync(brand);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.SaveChangesAsync();

        // کالای دارای کمبود موجودی (آستانه ۱۰، موجودی ۶)
        var lowStockProduct = new Product(
            name: "شمع پایه کوتاه",
            internalCode: "SP-LOW",
            model: "۲۰۶",
            purchasePrice: 80_000,
            salePrice: 120_000,
            categoryId: category.Id,
            brandId: brand.Id,
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
            categoryId: category.Id,
            brandId: brand.Id,
            defaultWarehouseId: warehouse.Id,
            isLowStockAlertEnabled: true,
            minStockThreshold: 5
        );

        await _context.Products.AddRangeAsync(lowStockProduct, normalStockProduct);
        await _context.SaveChangesAsync();

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

    [Fact]
    public async Task AdjustProductsPriceAsync_IncreasePrice_ShouldUpdateSalePriceAndRecordHistory()
    {
        // Arrange: ایجاد کالای پایه با قیمت خرید 100,000 و قیمت فروش فعلی 150,000
        var product = new Product("لنت ترمز جلو", "PAD-01", "پژو", 100_000, 150_000, Guid.NewGuid(), Guid.NewGuid());
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        // سناریو: افزایش ۲۰ درصدی قیمت فروش (150,000 + 30,000 = 180,000)
        var newSalePrice = 180_000m;
        var dto = new BatchPriceAdjustmentDto
        {
            ChangeType = PriceChangeType.BatchPercentage,
            Reason = "افزایش ۲۰ درصدی به دلیل تورم",
            Items = new List<PriceAdjustmentItemUpdateDto>
            {
                new() { ProductId = product.Id, NewSalePrice = newSalePrice }
            }
        };

        // Act
        await _service.AdjustProductsPriceAsync(dto);

        // Assert
        var updatedProduct = await _context.Products.FindAsync(product.Id);
        updatedProduct.Should().NotBeNull();
        updatedProduct!.SalePrice.Should().Be(180_000);

        // بررسی ثبت رکورد تاریخچه تغییر قیمت
        var history = await _context.PriceChangeHistories.FirstOrDefaultAsync(h => h.ProductId == product.Id);
        history.Should().NotBeNull();
        history!.OldSalePrice.Should().Be(150_000);
        history.NewSalePrice.Should().Be(180_000);
        history.ChangeType.Should().Be(PriceChangeType.BatchPercentage);
        history.Reason.Should().Be("افزایش ۲۰ درصدی به دلیل تورم");
    }

    [Fact]
    public async Task AdjustProductsPriceAsync_DecreasePrice_WhenAbovePurchasePrice_ShouldSucceed()
    {
        // Arrange: قیمت خرید 100,000 و قیمت فروش فعلی 200,000
        var product = new Product("شمع ایریدیوم", "SPK-01", "دنا", 100_000, 200_000, Guid.NewGuid(), Guid.NewGuid());
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        // سناریو: کاهش قیمت به 160,000 (همچنان بالاتر از قیمت خرید 100,000)
        var dto = new BatchPriceAdjustmentDto
        {
            ChangeType = PriceChangeType.BatchFixedAmount,
            Reason = "تخفیف ویژه فروش فوق‌العاده",
            Items = new List<PriceAdjustmentItemUpdateDto>
            {
                new() { ProductId = product.Id, NewSalePrice = 160_000 }
            }
        };

        // Act
        await _service.AdjustProductsPriceAsync(dto);

        // Assert
        var updatedProduct = await _context.Products.FindAsync(product.Id);
        updatedProduct!.SalePrice.Should().Be(160_000);

        var history = await _context.PriceChangeHistories.FirstOrDefaultAsync(h => h.ProductId == product.Id);
        history.Should().NotBeNull();
        history!.OldSalePrice.Should().Be(200_000);
        history.NewSalePrice.Should().Be(160_000);
    }

    [Fact]
    public async Task AdjustProductsPriceAsync_WithRoundingLogic_ShouldSaveRoundedPrice()
    {
        // Arrange: قیمت پایه 123,450 تومان
        var product = new Product("فیلتر بنزین", "FLT-GAS", "پراید", 80_000, 123_450, Guid.NewGuid(), Guid.NewGuid());
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        // سناریو گرد کردن به نزدیک‌ترین ۱,۰۰۰ تومان (مثلاً افزایش ۱۰ درصدی + گردکردن رو به بالا به هزارگان: 135,795 -> 136,000)
        var calculatedPrice = 123_450m * 1.10m; // 135795
        var roundedPrice = Math.Ceiling(calculatedPrice / 1_000m) * 1_000m; // 136000

        var dto = new BatchPriceAdjustmentDto
        {
            ChangeType = PriceChangeType.BatchPercentage,
            Reason = "افزایش با گرد کردن به نزدیک‌ترین ۱۰۰۰ تومان",
            Items = new List<PriceAdjustmentItemUpdateDto>
            {
                new() { ProductId = product.Id, NewSalePrice = roundedPrice }
            }
        };

        // Act
        await _service.AdjustProductsPriceAsync(dto);

        // Assert
        var updatedProduct = await _context.Products.FindAsync(product.Id);
        updatedProduct!.SalePrice.Should().Be(136_000);
    }

    [Fact]
    public async Task AdjustProductsPriceAsync_WhenNewPriceIsNegative_ShouldThrowException()
    {
        // Arrange: قیمت خرید 50,000 و قیمت فروش 70,000
        var product = new Product("بست لوله", "CLP-01", "تیبا", 50_000, 70_000, Guid.NewGuid(), Guid.NewGuid());
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        // سناریو: تلاش برای ثبت قیمت منفی (-10,000)
        var dto = new BatchPriceAdjustmentDto
        {
            ChangeType = PriceChangeType.BatchFixedAmount,
            Items = new List<PriceAdjustmentItemUpdateDto>
            {
                new() { ProductId = product.Id, NewSalePrice = -10_000m }
            }
        };

        // Act
        var act = async () => await _service.AdjustProductsPriceAsync(dto);

        // Assert: جلوگیری از قیمت منفی به دلیل کمتر بودن از قیمت خرید (و عدم مجاز بودن قیمت منفی)
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*نمی‌تواند کمتر از قیمت خرید*");

        // اطمینان از عدم تغییر قیمت در دیتابیس
        var unchangedProduct = await _context.Products.FindAsync(product.Id);
        unchangedProduct!.SalePrice.Should().Be(70_000);
    }

    [Fact]
    public async Task
        AdjustProductsPriceAsync_WhenNewSalePriceIsLessThanPurchasePrice_ShouldThrowInvalidOperationException()
    {
        // Arrange: قیمت خرید 200,000 و قیمت فروش 250,000
        var product = new Product("تسمه تایم", "TB-200", "سمند", 200_000, 250_000, Guid.NewGuid(), Guid.NewGuid());
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        // سناریو: تلاش برای کاهش قیمت فروش به کمتر از قیمت خرید (180,000 کمتر از 200,000)
        var dto = new BatchPriceAdjustmentDto
        {
            ChangeType = PriceChangeType.BatchPercentage,
            Reason = "تخفیف غیراصولی",
            Items = new List<PriceAdjustmentItemUpdateDto>
            {
                new() { ProductId = product.Id, NewSalePrice = 180_000 }
            }
        };

        // Act
        var act = async () => await _service.AdjustProductsPriceAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*قیمت فروش جدید کالا '{product.Name}'*نمی‌تواند کمتر از قیمت خرید آن*");

        // بررسی اینکه قیمت قبلی دست‌نخورده باقی مانده باشد
        var unchangedProduct = await _context.Products.FindAsync(product.Id);
        unchangedProduct!.SalePrice.Should().Be(250_000);

        // نباید هیچ رکوردی در تاریخچه ثبت شده باشد
        var historiesCount = await _context.PriceChangeHistories.CountAsync();
        historiesCount.Should().Be(0);
    }

    [Fact]
    public async Task AdjustProductsPriceAsync_WhenPriceIsUnchanged_ShouldIgnoreAndNotRecordHistory()
    {
        // Arrange: قیمت فعلی 300,000
        var product = new Product("رادیاتور آب", "RAD-01", "۲۰۶", 220_000, 300_000, Guid.NewGuid(), Guid.NewGuid());
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        // سناریو: ارسال همان قیمت قبلی
        var dto = new BatchPriceAdjustmentDto
        {
            ChangeType = PriceChangeType.BatchPercentage,
            Items = new List<PriceAdjustmentItemUpdateDto>
            {
                new() { ProductId = product.Id, NewSalePrice = 300_000 }
            }
        };

        // Act
        await _service.AdjustProductsPriceAsync(dto);

        // Assert: هیچ رکوردی نباید اضافه شده باشد
        var historiesCount = await _context.PriceChangeHistories.CountAsync();
        historiesCount.Should().Be(0);
    }

    [Fact]
    public async Task AdjustProductsPriceAsync_WithMultipleProducts_ShouldUpdateAllCorrectly()
    {
        // Arrange: دو کالای مختلف
        var p1 = new Product("کالای اول", "C1", "M1", 100_000, 120_000, Guid.NewGuid(), Guid.NewGuid());
        var p2 = new Product("کالای دوم", "C2", "M2", 200_000, 240_000, Guid.NewGuid(), Guid.NewGuid());
        await _context.Products.AddRangeAsync(p1, p2);
        await _context.SaveChangesAsync();

        // سناریو: تغییر قیمت گروهی هردو کالا
        var dto = new BatchPriceAdjustmentDto
        {
            ChangeType = PriceChangeType.BatchFixedAmount,
            Reason = "تعدیل قیمت گروهی پایان سال",
            Items = new List<PriceAdjustmentItemUpdateDto>
            {
                new() { ProductId = p1.Id, NewSalePrice = 140_000 },
                new() { ProductId = p2.Id, NewSalePrice = 280_000 }
            }
        };

        // Act
        await _service.AdjustProductsPriceAsync(dto);

        // Assert
        var updatedP1 = await _context.Products.FindAsync(p1.Id);
        var updatedP2 = await _context.Products.FindAsync(p2.Id);

        updatedP1!.SalePrice.Should().Be(140_000);
        updatedP2!.SalePrice.Should().Be(280_000);

        var histories = await _context.PriceChangeHistories.ToListAsync();
        histories.Should().HaveCount(2);
    }
}
