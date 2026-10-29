using AutoSpare.Application.DailySettlements.DTOs;
using AutoSpare.Application.Purchases.DTOs;
using AutoSpare.Application.Sales.DTOs;
using AutoSpare.Domain.Brands;
using AutoSpare.Domain.Categories;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Suppliers;
using AutoSpare.Domain.Warehouses;
using AutoSpare.Infrastructure.Persistence;
using AutoSpare.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Tests.Services;

public class ShopMultiDayScenarioTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PurchaseService _purchaseService;
    private readonly SaleService _saleService;
    private readonly DailySettlementService _settlementService;

    public ShopMultiDayScenarioTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x =>
                x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _purchaseService = new PurchaseService(_context);
        _saleService = new SaleService(_context);
        _settlementService = new DailySettlementService(_context);
    }

    [Fact]
    public async Task MultiDayShop_CompleteScenario_PurchaseSaleCancelAndSettlement_ShouldMatchExpectations()
    {
        // ==========================================
        // 0. آماده‌سازی داده‌های پایه (محیط مغازه)
        // ==========================================
        var supplier = new Supplier("تجهیزات یدکی پایتخت", "02144444444", "تهران");
        var warehouse = new Warehouse("انبار مرکزی", "تهران");
        var category = new Category("قطعات مصرفی", "Consumables");
        var brand = new Brand("سایپا", "Saipa");

        await _context.Suppliers.AddAsync(supplier);
        await _context.Warehouses.AddAsync(warehouse);
        await _context.Categories.AddAsync(category);
        await _context.Brands.AddAsync(brand);
        await _context.SaveChangesAsync();

        var product = new Product(
            name: "صفحه کلاج",
            internalCode: "CLU-100",
            model: "پراید",
            purchasePrice: 1_000_000m,
            salePrice: 1_400_000m,
            categoryId: category.Id,
            brandId: brand.Id,
            defaultWarehouseId: warehouse.Id
        );
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        var productId = product.Id;
        var warehouseId = warehouse.Id;
        var supplierId = supplier.Id;

        // تاریخ‌های پایه برای ۳ روز شبیه‌سازی
        var day1 = new DateTime(2026, 6, 1);
        var day2 = new DateTime(2026, 6, 2);
        var day3 = new DateTime(2026, 6, 3);

        // ==========================================
        // 1. روز اول: خرید اولیه و فروش به مشتری
        // ==========================================

        // الف) خرید ۲۰ عدد صفحه کلاج از تأمین‌کننده
        var purchaseDto = new CreatePurchaseDto
        {
            InvoiceNumber = "PUR-01",
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            PurchaseDate = day1,
            FinalizeImmediately = true,
            Items = new List<CreatePurchaseItemDto>
            {
                new() { ProductId = productId, Quantity = 20, UnitPrice = 1_000_000m }
            }
        };
        var purchaseId = await _purchaseService.CreatePurchaseAsync(purchaseDto);
        purchaseId.Should().NotBeEmpty();

        // بررسی موجودی انبار پس از خرید (باید ۲۰ عدد باشد)
        var invAfterPurchase = await _context.Inventories
            .FirstAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId);
        invAfterPurchase.Quantity.Should().Be(20);

        // ب) فروش ۵ عدد به مشتری
        var saleDto1 = new CreateSaleDto
        {
            InvoiceNumber = "INV-01",
            CustomerName = "آقای احمدی",
            WarehouseId = warehouseId,
            SaleDate = day1,
            FinalizeImmediately = true,
            Items = new List<CreateSaleItemDto>
            {
                new() { ProductId = productId, WarehouseId = warehouseId, Quantity = 5, UnitPrice = 1_400_000m }
            }
        };
        var saleId1 = await _saleService.CreateSaleAsync(saleDto1);
        saleId1.Should().NotBeEmpty();

        // موجودی انبار پس از فروش: ۲۰ - ۵ = ۱۵
        invAfterPurchase = await _context.Inventories
            .FirstAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId);
        invAfterPurchase.Quantity.Should().Be(15);

        // ج) ثبت تسویه روز اول (فروش ۵ عدد * ۱,۴۰۰,۰۰۰ = ۷,۰۰۰,۰۰۰ تومان)
        // مثلاً ۵ میلیون کارت‌خوان ۱ و ۲ میلیون وجه نقد
        var settlementDto1 = new UpsertDailySettlementDto
        {
            SettlementDate = day1,
            Pos1Amount = 5_000_000m,
            Pos2Amount = 0m,
            CashAmount = 2_000_000m,
            Notes = "تسویه روز اول کاری"
        };
        var settlementId1 = await _settlementService.UpsertAsync(settlementDto1);
        settlementId1.Should().NotBeEmpty();

        var savedSettlement1 = await _context.DailySettlements.FindAsync(settlementId1);
        savedSettlement1!.TotalAmount.Should().Be(7_000_000m);


        // ==========================================
        // 2. روز دوم: فروش جدید، ابطال فاکتور و خرید مجدد
        // ==========================================

        // الف) ثبت یک فروش موقت یا قطعی دیگر (مثلاً ۳ عدد)
        var saleDto2 = new CreateSaleDto
        {
            InvoiceNumber = "INV-02",
            CustomerName = "شرکت تاکسی‌رانی",
            WarehouseId = warehouseId,
            SaleDate = day2,
            FinalizeImmediately = true,
            Items = new List<CreateSaleItemDto>
            {
                new() { ProductId = productId, WarehouseId = warehouseId, Quantity = 3, UnitPrice = 1_400_000m }
            }
        };
        var saleId2 = await _saleService.CreateSaleAsync(saleDto2);

        // موجودی انبار موقتاً: ۱۵ - ۳ = ۱۲
        var invAfterSale2 = await _context.Inventories
            .FirstAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId);
        invAfterSale2.Quantity.Should().Be(12);

        // ب) ابطال فاکتور INV-02 به دلیل انصراف مشتری (موجودی باید برگردد به ۱۵)
        await _saleService.CancelSaleAsync(saleId2, "انصراف مشتری از خرید قطعات");

        var invAfterCancel = await _context.Inventories
            .FirstAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId);
        invAfterCancel.Quantity.Should().Be(15);

        // ج) ثبت تسویه روز دوم (چون فاکتور روز دوم باطل شد، تسویه نقدی/کارت صفر یا مبالغ دیگر)
        var settlementDto2 = new UpsertDailySettlementDto
        {
            SettlementDate = day2,
            Pos1Amount = 0m,
            Pos2Amount = 0m,
            CashAmount = 0m,
            Notes = "روز دوم - بدون فروش نهایی (ابطال فاکتور)"
        };
        await _settlementService.UpsertAsync(settlementDto2);


        // ==========================================
        // 3. روز سوم: بررسی نهایی، گزارش‌گیری و تسویه پایانی
        // ==========================================

        // الف) خرید ۱۰ عدد دیگر
        var purchaseDto2 = new CreatePurchaseDto
        {
            InvoiceNumber = "PUR-02",
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            PurchaseDate = day3,
            FinalizeImmediately = true,
            Items = new List<CreatePurchaseItemDto>
            {
                new() { ProductId = productId, Quantity = 10, UnitPrice = 1_050_000m }
            }
        };
        await _purchaseService.CreatePurchaseAsync(purchaseDto2);

        // موجودی نهایی انبار باید بشود: ۱۵ (موجودی قبلی) + ۱۰ (خرید جدید) = ۲۵
        var finalInventory = await _context.Inventories
            .FirstAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId);
        finalInventory.Quantity.Should().Be(25);

        // ب) فروش ۱۰ عدد در روز سوم (۱۰ * ۱,۴۰۰,۰۰۰ = ۱۴,۰۰۰,۰۰۰ تومان)
        var saleDto3 = new CreateSaleDto
        {
            InvoiceNumber = "INV-03",
            CustomerName = "مشتری عمده",
            WarehouseId = warehouseId,
            SaleDate = day3,
            FinalizeImmediately = true,
            Items = new List<CreateSaleItemDto>
            {
                new() { ProductId = productId, WarehouseId = warehouseId, Quantity = 10, UnitPrice = 1_400_000m }
            }
        };
        await _saleService.CreateSaleAsync(saleDto3);

        // ج) ثبت تسویه روز سوم
        var settlementDto3 = new UpsertDailySettlementDto
        {
            SettlementDate = day3,
            Pos1Amount = 10_000_000m,
            Pos2Amount = 4_000_000m,
            CashAmount = 0m,
            Notes = "تسویه روز سوم - فروش عمده"
        };
        var settlementId3 = await _settlementService.UpsertAsync(settlementDto3);

        var savedSettlement3 = await _context.DailySettlements.FindAsync(settlementId3);
        savedSettlement3!.TotalAmount.Should().Be(14_000_000m);

        // د) بررسی کلی تعداد کل تسویه‌های ثبت شده در سیستم (باید ۳ روز تسویه مجزا داشته باشیم)
        var allSettlements = await _settlementService.GetAllAsync();
        allSettlements.Should().HaveCount(3);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
