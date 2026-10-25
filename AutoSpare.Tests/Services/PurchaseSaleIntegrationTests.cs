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
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Tests.Services;

/// <summary>
/// تست‌های یکپارچگی (Integration Tests) برای سناریوهای متوالی خرید، فروش، ابطال و زنجیره گردش کاردکس انبار
/// </summary>
public class PurchaseSaleIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PurchaseService _purchaseService;
    private readonly SaleService _saleService;

    public PurchaseSaleIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"IntegrationDb_{Guid.NewGuid()}")
            .ConfigureWarnings(x =>
                x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _purchaseService = new PurchaseService(_context);
        _saleService = new SaleService(_context);
    }

    private async Task<(Guid SupplierId, Guid WarehouseId, Guid ProductId)> SeedMasterDataAsync()
    {
        var supplier = new Supplier("تأمین‌کننده قطعات یدکی مرکزی", "02177889900", "تهران، چراغ‌برق");
        var warehouse = new Warehouse("انبار پخش مرکزی", "تهران، خیابان امیرکبیر");
        var category = new Category("سیستم ترمز و انتقال قدرت");
        var brand = new Brand("ایساکو");

        await _context.Suppliers.AddAsync(supplier);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Categories.AddAsync(category);
        await _context.Brands.AddAsync(brand);
        await _context.SaveChangesAsync();

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
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        return (supplier.Id, warehouse.Id, product.Id);
    }

    [Fact]
    public async Task CompleteLifeCycle_PurchaseThenSaleThenCancelSale_InventoryAndMovementsMustMatch()
    {
        // Arrange
        var (supplierId, warehouseId, productId) = await SeedMasterDataAsync();

        // گام ۱: ثبت و نهایی‌سازی خرید به تعداد ۲۰ عدد
        var purchaseDto = new CreatePurchaseDto
        {
            InvoiceNumber = "INT-PUR-001",
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            FinalizeImmediately = true,
            Items = new List<CreatePurchaseItemDto>
            {
                new() { ProductId = productId, Quantity = 20, UnitPrice = 420_000m }
            }
        };

        var purchaseId = await _purchaseService.CreatePurchaseAsync(purchaseDto, "مدیر خرید");
        purchaseId.Should().NotBeEmpty();

        var inventoryAfterPurchase = await _context.Inventories
            .Include(i => i.Movements)
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId);

        inventoryAfterPurchase.Should().NotBeNull();
        inventoryAfterPurchase!.Quantity.Should().Be(20);

        // گام ۲: ثبت فاکتور فروش به تعداد ۷ عدد
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

        var saleId = await _saleService.CreateSaleAsync(saleDto);
        saleId.Should().NotBeEmpty();

        var inventoryAfterSale = await _context.Inventories
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId);

        inventoryAfterSale!.Quantity.Should().Be(13);

        // گام ۳: ابطال فاکتور فروش و بازگشت موجودی
        await _saleService.CancelSaleAsync(saleId);

        var finalInventory = await _context.Inventories
            .Include(i => i.Movements)
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId);

        finalInventory!.Quantity.Should().Be(20);

        // بررسی زنجیره کاردکس انبار (Movements)
        var movements = finalInventory.Movements.OrderBy(m => m.CreatedAt).ToList();
        movements.Should().HaveCount(3);

        movements[0].Type.Should().Be(StockMovementType.Purchase);
        movements[0].Reference.Should().Be("INT-PUR-001");

        movements[1].Type.Should().Be(StockMovementType.Sale);
        movements[1].Reference.Should().Be("INT-SALE-001");

        movements[2].Type.Should().Be(StockMovementType.CustomerReturn);
        movements[2].Reference.Should().Be("INT-SALE-001");
    }

    [Fact]
    public async Task PurchaseDraft_Cancelled_CannotBeSoldDueToInsufficientStock()
    {
        // Arrange
        var (supplierId, warehouseId, productId) = await SeedMasterDataAsync();

        var purchaseDto = new CreatePurchaseDto
        {
            InvoiceNumber = "INT-PUR-DRAFT-01",
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            FinalizeImmediately = false,
            Items = new List<CreatePurchaseItemDto>
            {
                new() { ProductId = productId, Quantity = 10, UnitPrice = 400_000m }
            }
        };

        var purchaseId = await _purchaseService.CreatePurchaseAsync(purchaseDto);
        await _purchaseService.CancelPurchaseAsync(purchaseId);

        var purchase = await _context.Purchases.FindAsync(purchaseId);
        purchase!.Status.Should().Be(PurchaseStatus.Cancelled);

        var saleDto = new CreateSaleDto
        {
            InvoiceNumber = "INT-SALE-FAIL-01",
            CustomerName = "مشتری متفرقه",
            WarehouseId = warehouseId,
            FinalizeImmediately = true,
            Items = new List<CreateSaleItemDto>
            {
                new() { ProductId = productId, WarehouseId = warehouseId, Quantity = 1, UnitPrice = 500_000m }
            }
        };

        var act = async () => await _saleService.CreateSaleAsync(saleDto);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
