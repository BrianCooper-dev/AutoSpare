using AutoSpare.Domain.Inventories;
using AutoSpare.Domain.Inventories.Enums;
using AutoSpare.Domain.Products;
using AutoSpare.Domain.Products.Enums;
using AutoSpare.Domain.Sales;
using AutoSpare.Domain.Sales.Enums;
using FluentAssertions;

namespace AutoSpare.Tests.Services;

public class DomainRulesTests
{
    #region Inventory

    [Fact]
    public void Inventory_ctor_should_create_initial_stock_movement_when_initial_quantity_positive()
    {
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        const int initialQty = 10;

        var inv = new Inventory(productId, warehouseId, initialQty);

        inv.ProductId.Should().Be(productId);
        inv.WarehouseId.Should().Be(warehouseId);
        inv.Quantity.Should().Be(initialQty);

        inv.Movements.Should().NotBeNull();
        inv.Movements.Should().HaveCount(1);

        var m = inv.Movements.Single();
        m.QuantityChange.Should().Be(initialQty);
        m.BalanceAfter.Should().Be(initialQty);
    }

    [Fact]
    public void Inventory_decrease_should_throw_when_result_would_be_negative()
    {
        var inv = new Inventory(Guid.NewGuid(), Guid.NewGuid(), 5);

        var act = () =>
            inv.DecreaseQuantity(
                amount: 6,
                type: StockMovementType.Sale,
                reason: "فروش",
                reference: "INV-1",
                performedBy: "tester"
            );

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*موجودی کافی نیست*");
    }

    [Fact]
    public void Inventory_adjust_with_no_change_should_not_create_movement()
    {
        var inv = new Inventory(Guid.NewGuid(), Guid.NewGuid(), 10);
        inv.Movements.Should().HaveCount(1);

        inv.AdjustQuantity(
            newQuantity: 10,
            reason: "انبارگردانی بدون تغییر",
            reference: "STK-0",
            performedBy: "tester"
        );

        inv.Quantity.Should().Be(10);
        inv.Movements.Should().HaveCount(1);
    }

    [Fact]
    public void Inventory_increase_should_create_new_movement_and_update_quantity()
    {
        var inv = new Inventory(Guid.NewGuid(), Guid.NewGuid(), 10);

        inv.IncreaseQuantity(
            amount: 3,
            type: StockMovementType.Purchase,
            reason: "خرید",
            reference: "PUR-1",
            performedBy: "tester"
        );

        inv.Quantity.Should().Be(13);
        inv.Movements.Should().HaveCount(2);

        var last = inv.Movements.Last();
        last.QuantityChange.Should().Be(3);
        last.BalanceAfter.Should().Be(13);
        last.PerformedBy.Should().Be("tester");
    }

    #endregion

    #region Product

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Product_ctor_should_throw_when_name_is_empty(string? name)
    {
        var act = () =>
            new Product(
                name: name!,
                internalCode: "P-001",
                model: "M1",
                purchasePrice: 1000,
                salePrice: 1500,
                categoryId: Guid.NewGuid(),
                brandId: Guid.NewGuid(),
                imagePath: null,
                defaultWarehouseId: Guid.NewGuid(),
                isLowStockAlertEnabled: false,
                minStockThreshold: 1
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Product_ctor_should_throw_when_defaultWarehouseId_is_empty()
    {
        var act = () =>
            new Product(
                name: "کالا",
                internalCode: "P-001",
                model: "M1",
                purchasePrice: 1000,
                salePrice: 1500,
                categoryId: Guid.NewGuid(),
                brandId: Guid.NewGuid(),
                imagePath: null,
                defaultWarehouseId: Guid.Empty,
                isLowStockAlertEnabled: false,
                minStockThreshold: 1
            );

        act.Should().Throw<ArgumentException>()
            .WithMessage("*شناسه انبار*");
    }

    [Fact]
    public void Product_change_status_should_update_last_modified_at()
    {
        var p = new Product(
            name: "کالا",
            internalCode: "P-001",
            model: "M1",
            purchasePrice: 1000,
            salePrice: 1500,
            categoryId: Guid.NewGuid(),
            brandId: Guid.NewGuid(),
            imagePath: null,
            defaultWarehouseId: Guid.NewGuid(),
            isLowStockAlertEnabled: false,
            minStockThreshold: 1
        );

        var before = p.LastModifiedAt;
        p.ChangeStatus(ProductStatus.Active);

        p.LastModifiedAt.Should().NotBe(before);
        p.LastModifiedAt.Should().NotBeNull();
    }

    #endregion

    #region Sale (Cancellation & Completion)

    [Fact]
    public void Sale_complete_should_throw_when_has_no_items()
    {
        var sale = new Sale(
            invoiceNumber: "INV-0001",
            customerName: "مشتری",
            warehouseId: null,
            saleDate: DateTime.UtcNow
        );

        var act = () => sale.Complete();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*بدون کالا*");
    }

    [Fact]
    public void Sale_add_or_update_item_should_merge_by_product_and_warehouse()
    {
        var sale = new Sale(
            invoiceNumber: "INV-0001",
            customerName: "مشتری",
            warehouseId: null,
            saleDate: DateTime.UtcNow
        );

        var productId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        sale.AddOrUpdateItem(productId, warehouseId: whId, quantity: 2, unitPrice: 1500, unitPurchasePrice: 1000);
        sale.AddOrUpdateItem(productId, warehouseId: whId, quantity: 3, unitPrice: 1500, unitPurchasePrice: 1000);

        sale.Items.Should().HaveCount(1);
        sale.Items.Single().Quantity.Should().Be(3); // آخرین مقدار ثبت‌شده در AddOrUpdate
    }

    [Fact]
    public void Sale_cancel_should_set_cancelledAt_and_default_reason_when_null()
    {
        var sale = new Sale(
            invoiceNumber: "INV-0001",
            customerName: "مشتری",
            warehouseId: null,
            saleDate: DateTime.UtcNow
        );

        sale.Cancel(null);

        sale.CancelledAt.Should().NotBeNull();
        sale.CancellationReason.Should().Be("بدون درج دلیل");
        sale.Status.Should().Be(SaleStatus.Cancelled);
    }

    [Fact]
    public void Sale_cancel_should_not_allow_double_cancel()
    {
        var sale = new Sale(
            invoiceNumber: "INV-0001",
            customerName: "مشتری",
            warehouseId: null,
            saleDate: DateTime.UtcNow
        );

        sale.Cancel("اشتباه ثبت شد");

        var act = () => sale.Cancel("دوباره");

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion
}
