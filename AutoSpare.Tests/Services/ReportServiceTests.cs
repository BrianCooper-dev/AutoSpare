using AutoSpare.Application.Reports.DTOs;
using AutoSpare.Application.Reports.Enums;
using AutoSpare.Domain.Brands;
using AutoSpare.Domain.Categories;
using AutoSpare.Domain.Inventories;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Purchases;
using AutoSpare.Domain.Sales;
using AutoSpare.Domain.Warehouses;
using AutoSpare.Infrastructure.Persistence;
using AutoSpare.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Tests.Services;

public class ReportServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ReportService _service;
    private static readonly TimeSpan IranOffset = TimeSpan.FromHours(3.5);

    public ReportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _service = new ReportService(_context);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_WithCompletedSalesAndPurchases_ShouldCalculateCorrectly()
    {
        // Arrange
        var warehouse = new Warehouse("انبار مرکزی", "تهران");
        var category = new Category("قطعات موتور");
        var brand = new Brand("ایران خودرو");
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Categories.AddAsync(category);
        await _context.Brands.AddAsync(brand);
        await _context.SaveChangesAsync();

        var product = new Product("واشر سر سیلندر", "GASKET-01", "TU5", 500_000m, 750_000m, category.Id, brand.Id, null, warehouse.Id);
        await _context.Products.AddAsync(product);

        var inventory = new Inventory(product.Id, warehouse.Id, 20);
        await _context.Inventories.AddAsync(inventory);

        var nowIran = DateTime.UtcNow.Add(IranOffset);
        var saleDateUtc = DateTime.SpecifyKind(nowIran.Date.AddHours(10) - IranOffset, DateTimeKind.Utc);

        var sale = new Sale("INV-001", "علی رضایی", warehouse.Id, saleDateUtc);
        sale.AddOrUpdateItem(product.Id, warehouse.Id, 2, 750_000m, 500_000m);
        sale.Complete();
        await _context.Sales.AddAsync(sale);

        var purchase = new Purchase("PUR-001", Guid.NewGuid(), warehouse.Id, saleDateUtc);
        purchase.AddOrUpdateItem(product.Id, 10, 500_000m);
        purchase.Complete();
        await _context.Purchases.AddAsync(purchase);

        await _context.SaveChangesAsync();

        // Act
        var summary = await _service.GetDashboardSummaryAsync();

        // Assert
        summary.Should().NotBeNull();
        summary.TodaySalesAmount.Should().Be(1_500_000m);
        summary.TodayProfitAmount.Should().Be(500_000m);
        summary.TodayCompletedSalesCount.Should().Be(1);
        summary.TodayPurchasesAmount.Should().Be(5_000_000m);

        summary.TotalStockItemsCount.Should().Be(20);
        summary.TotalInventoryPurchaseValue.Should().Be(10_000_000m);
        summary.TotalInventorySaleValue.Should().Be(15_000_000m);
    }

    [Fact]
    public async Task GetDailySalesSummaryAsync_ShouldGroupSalesByIranDateCorrectly()
    {
        // Arrange
        var warehouse = new Warehouse("انبار مرکزی", "تهران");
        var category = new Category("قطعات مصرفی");
        var brand = new Brand("ایران خودرو");
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Categories.AddAsync(category);
        await _context.Brands.AddAsync(brand);
        await _context.SaveChangesAsync();

        var product = new Product("فیلتر روغن", "FLT-01", "۲۰۶", 100_000m, 150_000m, category.Id, brand.Id, null, warehouse.Id);
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        var sampleSaleDateUtc = DateTime.SpecifyKind(new DateTime(2024, 5, 4, 10, 0, 0) - IranOffset, DateTimeKind.Utc);

        var sale = new Sale("INV-100", "محمد تقوی", warehouse.Id, sampleSaleDateUtc);
        sale.AddOrUpdateItem(product.Id, warehouse.Id, 4, 150_000m, 100_000m);
        sale.Complete();
        await _context.Sales.AddAsync(sale);
        await _context.SaveChangesAsync();

        var filter = new ReportFilterDto();

        // Act
        var result = await _service.GetDailySalesSummaryAsync(filter);

        // Assert
        result.Should().ContainSingle();
        var item = result.First();
        item.InvoiceCount.Should().Be(1);
        item.TotalQuantitySold.Should().Be(4);
        item.TotalSales.Should().Be(600_000m);
        item.TotalCost.Should().Be(400_000m);
        item.TotalProfit.Should().Be(200_000m);
    }

    [Fact]
    public async Task GetTopSellingProductsAsync_ShouldReturnProductsSortedByQuantityOrSales()
    {
        // Arrange
        var warehouse = new Warehouse("انبار مرکزی", "تهران");
        var category = new Category("لوازم یدکی");
        var brand = new Brand("سایپا");
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Categories.AddAsync(category);
        await _context.Brands.AddAsync(brand);
        await _context.SaveChangesAsync();

        var p1 = new Product("لنت ترمز", "BRK-01", "پراید", 200_000m, 300_000m, category.Id, brand.Id, null, warehouse.Id);
        var p2 = new Product("شمع موتور", "SPK-02", "پراید", 100_000m, 160_000m, category.Id, brand.Id, null, warehouse.Id);
        await _context.Products.AddRangeAsync(p1, p2);
        await _context.SaveChangesAsync();

        var sale = new Sale("INV-200", "رضا حدادی", warehouse.Id, DateTime.UtcNow);
        sale.AddOrUpdateItem(p1.Id, warehouse.Id, 2, 300_000m, 200_000m);
        sale.AddOrUpdateItem(p2.Id, warehouse.Id, 5, 160_000m, 100_000m);
        sale.Complete();
        await _context.Sales.AddAsync(sale);
        await _context.SaveChangesAsync();

        // Act & Assert 1
        var topByQty = await _service.GetTopSellingProductsAsync(10, TopSellingSortMode.ByQuantity);
        topByQty.Should().HaveCount(2);
        topByQty[0].ProductName.Should().Be("شمع موتور");
        topByQty[0].TotalQuantitySold.Should().Be(5);

        // Act & Assert 2
        var topBySales = await _service.GetTopSellingProductsAsync(10, TopSellingSortMode.BySalesAmount);
        topBySales[0].ProductName.Should().Be("شمع موتور");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
