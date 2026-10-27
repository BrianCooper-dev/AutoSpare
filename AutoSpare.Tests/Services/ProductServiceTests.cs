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
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _service = new ProductService(_context);
    }

    private static CreateProductDto ValidCreateDto() => new()
    {
        Name = "Test product",
        InternalCode = "TEST-001",
        Model = "TU5",
        PurchasePrice = 100_000m,
        SalePrice = 150_000m,
        CategoryId = Guid.NewGuid(),
        BrandId = Guid.NewGuid()
    };

    private static UpdateProductDto ValidUpdateDto(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        InternalCode = product.InternalCode,
        Model = product.Model,
        PurchasePrice = product.PurchasePrice,
        SalePrice = product.SalePrice,
        CategoryId = product.CategoryId,
        BrandId = product.BrandId,
        DefaultWarehouseId = product.DefaultWarehouseId,
        ImagePath = product.ImagePath,
        IsLowStockAlertEnabled = product.IsLowStockAlertEnabled,
        MinStockThreshold = product.MinStockThreshold
    };

    private async Task<Product> SeedProductAsync(
        string code = "TEST-001",
        decimal purchasePrice = 100_000m,
        decimal salePrice = 150_000m)
    {
        var product = new Product(
            "Test product",
            code,
            "TU5",
            purchasePrice,
            salePrice,
            Guid.NewGuid(),
            Guid.NewGuid());

        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();
        return product;
    }

    private static BatchPriceAdjustmentDto PriceRequest(
        Guid productId,
        decimal newPrice,
        PriceChangeType changeType = PriceChangeType.BatchFixedAmount,
        string? reason = null) => new()
    {
        ChangeType = changeType,
        Reason = reason,
        Items = new List<PriceAdjustmentItemUpdateDto>
        {
            new() { ProductId = productId, NewSalePrice = newPrice }
        }
    };

    [Fact]
    public async Task CreateProduct_WithUniqueInternalCode_ShouldSucceed()
    {
        var dto = ValidCreateDto();

        var id = await _service.CreateProductAsync(dto);

        id.Should().NotBeEmpty();
        _context.ChangeTracker.Clear();
        var saved = await _context.Products.FindAsync(id);
        saved.Should().NotBeNull();
        saved!.InternalCode.Should().Be(dto.InternalCode);
        saved.Name.Should().Be(dto.Name);
        saved.SalePrice.Should().Be(dto.SalePrice);
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateInternalCode_ShouldThrowInvalidOperationException()
    {
        await SeedProductAsync();
        var dto = ValidCreateDto();

        Func<Task> act = async () => await _service.CreateProductAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*قبلاً در سیستم ثبت شده است*");

        _context.ChangeTracker.Clear();
        (await _context.Products.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateProduct_WithInitialStockAndWarehouse_ShouldCreateInventoryRecord()
    {
        var warehouse = new Warehouse("Central warehouse", "Tehran");
        await _context.Warehouses.AddAsync(warehouse);
        await _context.SaveChangesAsync();

        var dto = ValidCreateDto();
        dto.DefaultWarehouseId = warehouse.Id;
        dto.InitialQuantity = 15;

        var id = await _service.CreateProductAsync(dto);

        _context.ChangeTracker.Clear();
        var inventory = await _context.Inventories.SingleAsync(i => i.ProductId == id && i.WarehouseId == warehouse.Id);

        inventory.Quantity.Should().Be(15);
    }

    [Fact]
    public async Task CreateProduct_WithInitialStock_WithoutWarehouse_ShouldThrowInvalidOperationException()
    {
        var dto = ValidCreateDto();
        dto.InitialQuantity = 5;

        Func<Task> act = async () => await _service.CreateProductAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*انتخاب انبار الزامی است*");

        _context.ChangeTracker.Clear();
        (await _context.Products.CountAsync()).Should().Be(0);
        (await _context.Inventories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateProduct_WithNegativeInitialQuantity_ShouldReject()
    {
        var dto = ValidCreateDto();
        dto.InitialQuantity = -1;

        Func<Task> act = async () => await _service.CreateProductAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*موجودی اولیه*");

        _context.ChangeTracker.Clear();
        (await _context.Products.CountAsync()).Should().Be(0);
    }

    public static IEnumerable<object[]> InvalidProductInputs()
    {
        yield return new object[] { "Name", null!, "نام کالا" };
        yield return new object[] { "Name", "", "نام کالا" };
        yield return new object[] { "Name", "   ", "نام کالا" };
        yield return new object[] { "Name", new string('A', 201), "نام کالا" };

        yield return new object[] { "InternalCode", null!, "کد فنی" };
        yield return new object[] { "InternalCode", "", "کد فنی" };
        yield return new object[] { "InternalCode", "   ", "کد فنی" };
        yield return new object[] { "InternalCode", new string('A', 51), "کد فنی" };

        yield return new object[] { "Model", new string('A', 151), "مدل" };
        yield return new object[] { "ImagePath", new string('A', 501), "مسیر تصویر" };
        yield return new object[] { "PurchasePrice", -1m, "قیمت خرید" };
        yield return new object[] { "SalePrice", -1m, "قیمت فروش" };
        yield return new object[] { "SalePrice", 99_999m, "قیمت فروش" };
        yield return new object[] { "MinStockThreshold", -1, "آستانه هشدار" };

        yield return new object[] { "CategoryId", null!, "دسته‌بندی" };
        yield return new object[] { "CategoryId", Guid.Empty, "دسته‌بندی" };
        yield return new object[] { "BrandId", null!, "برند" };
        yield return new object[] { "BrandId", Guid.Empty, "برند" };
    }

    private static void SetProperty(object dto, string property, object? value)
    {
        var info = dto.GetType().GetProperty(property)
                   ?? throw new InvalidOperationException($"Unknown property: {property}");

        info.SetValue(dto, value);
    }

    [Theory]
    [MemberData(nameof(InvalidProductInputs))]
    public async Task CreateProduct_WithInvalidInput_ShouldRejectWithoutSaving(
        string property,
        object? value,
        string message)
    {
        var dto = ValidCreateDto();
        SetProperty(dto, property, value);

        Func<Task> act = async () => await _service.CreateProductAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"*{message}*");

        _context.ChangeTracker.Clear();
        (await _context.Products.CountAsync()).Should().Be(0);
        (await _context.Inventories.CountAsync()).Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(InvalidProductInputs))]
    public async Task UpdateProduct_WithInvalidInput_ShouldRejectWithoutChangingProduct(
        string property,
        object? value,
        string message)
    {
        var product = await SeedProductAsync();
        var dto = ValidUpdateDto(product);
        SetProperty(dto, property, value);

        Func<Task> act = () => _service.UpdateProductAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"*{message}*");

        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var saved = await _context.Products.SingleAsync();
        saved.Name.Should().Be("Test product");
        saved.InternalCode.Should().Be("TEST-001");
        saved.Model.Should().Be("TU5");
        saved.ImagePath.Should().BeNull();
        saved.PurchasePrice.Should().Be(100_000m);
        saved.SalePrice.Should().Be(150_000m);
        saved.MinStockThreshold.Should().Be(0);
        saved.CategoryId.Should().Be(dto.CategoryId == product.CategoryId
            ? dto.CategoryId!.Value
            : product.CategoryId);
        saved.BrandId.Should().Be(product.BrandId);
    }

    [Fact]
    public async Task CreateProduct_WithNullDto_ShouldThrowArgumentNullException()
    {
        Func<Task> act = async () => await _service.CreateProductAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateProduct_WithNullDto_ShouldThrowArgumentNullException()
    {
        Func<Task> act = () => _service.UpdateProductAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateProduct_WithMaximumTextLengths_ShouldSucceed()
    {
        var dto = ValidCreateDto();
        dto.Name = new string('N', 200);
        dto.InternalCode = new string('C', 50);
        dto.Model = new string('M', 150);

        var id = await _service.CreateProductAsync(dto);

        _context.ChangeTracker.Clear();
        var saved = await _context.Products.SingleAsync(p => p.Id == id);
        saved.Name.Should().HaveLength(200);
        saved.InternalCode.Should().HaveLength(50);
        saved.Model.Should().HaveLength(150);
    }

    [Fact]
    public async Task CreateProduct_WithZeroPricesAndQuantities_ShouldSucceed()
    {
        var dto = ValidCreateDto();
        dto.PurchasePrice = 0;
        dto.SalePrice = 0;
        dto.MinStockThreshold = 0;
        dto.InitialQuantity = 0;

        var id = await _service.CreateProductAsync(dto);

        id.Should().NotBeEmpty();
        _context.ChangeTracker.Clear();
        var saved = await _context.Products.SingleAsync();
        saved.PurchasePrice.Should().Be(0);
        saved.SalePrice.Should().Be(0);
        (await _context.Inventories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UpdateProduct_WithValidInput_ShouldSucceed()
    {
        var product = await SeedProductAsync();
        var dto = ValidUpdateDto(product);
        dto.Name = "Updated product";
        dto.Model = "EF7";
        dto.PurchasePrice = 120_000m;
        dto.SalePrice = 180_000m;
        dto.IsLowStockAlertEnabled = true;
        dto.MinStockThreshold = 10;

        await _service.UpdateProductAsync(dto);

        _context.ChangeTracker.Clear();
        var saved = await _context.Products.SingleAsync();
        saved.Name.Should().Be(dto.Name);
        saved.Model.Should().Be(dto.Model);
        saved.PurchasePrice.Should().Be(120_000m);
        saved.SalePrice.Should().Be(180_000m);
        saved.IsLowStockAlertEnabled.Should().BeTrue();
        saved.MinStockThreshold.Should().Be(10);
    }

    [Fact]
    public async Task UpdateProduct_WithSameInternalCode_ShouldSucceed()
    {
        var product = await SeedProductAsync();
        var dto = ValidUpdateDto(product);

        await _service.UpdateProductAsync(dto);

        _context.ChangeTracker.Clear();
        (await _context.Products.SingleAsync()).InternalCode.Should().Be("TEST-001");
    }

    [Fact]
    public async Task UpdateProduct_WithDuplicateInternalCode_ShouldReject()
    {
        await SeedProductAsync("FIRST");
        var second = await SeedProductAsync("SECOND");
        var dto = ValidUpdateDto(second);
        dto.InternalCode = "FIRST";

        Func<Task> act = () => _service.UpdateProductAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*قبلاً برای کالای دیگری ثبت شده است*");

        _context.ChangeTracker.Clear();
        var saved = await _context.Products.SingleAsync(p => p.Id == second.Id);
        saved.InternalCode.Should().Be("SECOND");
    }

    [Fact]
    public async Task UpdateProduct_WhenProductDoesNotExist_ShouldReject()
    {
        var source = await SeedProductAsync();
        var dto = ValidUpdateDto(source);
        dto.Id = Guid.NewGuid();

        Func<Task> act = () => _service.UpdateProductAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*کالای مورد نظر یافت نشد*");
    }

    private async Task SeedStockProductsAsync()
    {
        var category = new Category("Brakes");
        var brand = new Brand("Test brand");
        var warehouse = new Warehouse("Central warehouse", "Tehran");

        await _context.Categories.AddAsync(category);
        await _context.Brands.AddAsync(brand);
        await _context.Warehouses.AddAsync(warehouse);

        var lowStock = new Product(
            "Low stock product", "LOW", "TU5", 100, 150,
            category.Id, brand.Id,
            defaultWarehouseId: warehouse.Id,
            isLowStockAlertEnabled: true,
            minStockThreshold: 10);

        var normalStock = new Product(
            "Normal stock product", "NORMAL", "TU5", 100, 150,
            category.Id, brand.Id,
            defaultWarehouseId: warehouse.Id,
            isLowStockAlertEnabled: true,
            minStockThreshold: 5);

        await _context.Products.AddRangeAsync(lowStock, normalStock);
        await _context.Inventories.AddRangeAsync(
            new Inventory(lowStock.Id, warehouse.Id, initialQuantity: 8),
            new Inventory(normalStock.Id, warehouse.Id, initialQuantity: 20));

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetProductsAsync_WhenStockIsLessThanOrEqualToThreshold_ShouldIdentifyAsLowStock()
    {
        await SeedStockProductsAsync();

        var products = await _service.GetProductsAsync(new ProductFilterDto());
        var item = products.Should().ContainSingle(p => p.InternalCode == "LOW").Subject;

        item.TotalStock.Should().Be(8);
        item.MinStockThreshold.Should().Be(10);
        item.IsLowStockAlertEnabled.Should().BeTrue();
        item.IsLowStock.Should().BeTrue();
    }

    [Fact]
    public async Task GetProductsAsync_WithLowStockFilter_ShouldReturnOnlyLowStockProducts()
    {
        await SeedStockProductsAsync();

        var result = await _service.GetProductsAsync(new ProductFilterDto
        {
            StockStatus = StockStatusFilter.LowStock
        });

        result.Should().ContainSingle().Which.InternalCode.Should().Be("LOW");
    }

    [Fact]
    public async Task AdjustProductsPriceAsync_IncreasePrice_ShouldUpdateSalePriceAndRecordHistory()
    {
        var product = await SeedProductAsync();
        var dto = PriceRequest(
            product.Id, 180_000m,
            PriceChangeType.BatchPercentage, "Increase by 20 percent");

        await _service.AdjustProductsPriceAsync(dto);

        _context.ChangeTracker.Clear();
        (await _context.Products.SingleAsync()).SalePrice.Should().Be(180_000m);

        var history = await _context.PriceChangeHistories.SingleAsync();
        history.ProductId.Should().Be(product.Id);
        history.OldPurchasePrice.Should().Be(100_000m);
        history.NewPurchasePrice.Should().Be(100_000m);
        history.OldSalePrice.Should().Be(150_000m);
        history.NewSalePrice.Should().Be(180_000m);
        history.ChangeType.Should().Be(dto.ChangeType);
        history.Reason.Should().Be(dto.Reason);
    }

    [Fact]
    public async Task AdjustProductsPriceAsync_DecreasePrice_WhenAbovePurchasePrice_ShouldSucceed()
    {
        var product = await SeedProductAsync(salePrice: 200_000m);

        await _service.AdjustProductsPriceAsync(
            PriceRequest(product.Id, 160_000m));

        _context.ChangeTracker.Clear();
        (await _context.Products.SingleAsync()).SalePrice.Should().Be(160_000m);
        var history = await _context.PriceChangeHistories.SingleAsync();
        history.OldSalePrice.Should().Be(200_000m);
        history.NewSalePrice.Should().Be(160_000m);
    }

    [Fact]
    public async Task AdjustProductsPriceAsync_WithPreRoundedPrice_ShouldSaveSuppliedPrice()
    {
        var product = await SeedProductAsync(
            purchasePrice: 80_000m, salePrice: 123_450m);

        var roundedPrice = Math.Ceiling(123_450m * 1.10m / 1_000m) * 1_000m;

        await _service.AdjustProductsPriceAsync(
            PriceRequest(product.Id, roundedPrice, PriceChangeType.BatchPercentage));

        _context.ChangeTracker.Clear();
        (await _context.Products.SingleAsync()).SalePrice.Should().Be(136_000m);
    }

    [Theory]
    [InlineData(-10_000)]
    [InlineData(99_999)]
    public async Task AdjustProductsPriceAsync_WhenNewPriceIsBelowPurchasePrice_ShouldReject(
        int newPrice)
    {
        var product = await SeedProductAsync();

        Func<Task> act = () => _service.AdjustProductsPriceAsync(
            PriceRequest(product.Id, newPrice));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*نمی‌تواند کمتر از قیمت خرید*");

        _context.ChangeTracker.Clear();
        (await _context.Products.SingleAsync()).SalePrice.Should().Be(150_000m);
        (await _context.PriceChangeHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AdjustProductsPriceAsync_WhenPriceIsUnchanged_ShouldIgnoreAndNotRecordHistory()
    {
        var product = await SeedProductAsync();

        await _service.AdjustProductsPriceAsync(
            PriceRequest(product.Id, product.SalePrice));

        _context.ChangeTracker.Clear();
        (await _context.Products.SingleAsync()).SalePrice.Should().Be(150_000m);
        (await _context.PriceChangeHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AdjustProductsPriceAsync_WithMultipleProducts_ShouldUpdateAllCorrectly()
    {
        var first = await SeedProductAsync("FIRST", 100_000m, 120_000m);
        var second = await SeedProductAsync("SECOND", 200_000m, 240_000m);

        var dto = new BatchPriceAdjustmentDto
        {
            ChangeType = PriceChangeType.BatchFixedAmount,
            Reason = "Batch adjustment",
            Items = new List<PriceAdjustmentItemUpdateDto>
            {
                new() { ProductId = first.Id, NewSalePrice = 140_000m },
                new() { ProductId = second.Id, NewSalePrice = 280_000m }
            }
        };

        await _service.AdjustProductsPriceAsync(dto);

        _context.ChangeTracker.Clear();
        var savedFirst = await _context.Products.SingleAsync(p => p.Id == first.Id);
        var savedSecond = await _context.Products.SingleAsync(p => p.Id == second.Id);
        savedFirst.SalePrice.Should().Be(140_000m);
        savedSecond.SalePrice.Should().Be(280_000m);
        (await _context.PriceChangeHistories.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task AdjustProductsPriceAsync_WithEmptyItems_ShouldReject()
    {
        var dto = new BatchPriceAdjustmentDto();

        Func<Task> act = () => _service.AdjustProductsPriceAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*هیچ کالایی*");
    }

    [Fact]
    public async Task AdjustProductsPriceAsync_WithMissingProduct_ShouldReject()
    {
        var dto = PriceRequest(Guid.NewGuid(), 150_000m);

        Func<Task> act = () => _service.AdjustProductsPriceAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*برخی از کالاهای انتخابی*");

        (await _context.PriceChangeHistories.CountAsync()).Should().Be(0);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
