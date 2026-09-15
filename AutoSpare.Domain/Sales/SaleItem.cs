using AutoSpare.Domain.Common;
using AutoSpare.Domain.Products;

namespace AutoSpare.Domain.Sales;

/// <summary>
/// ردیف کالای فروخته‌شده در فاکتور فروش
/// </summary>
public class SaleItem : BaseEntity
{
    public Guid SaleId { private set; get; }

    public Guid ProductId { private set; get; }
    public Product? Product { private set; get; }

    /// <summary>
    /// تعداد فروخته شده
    /// </summary>
    public int Quantity { private set; get; }

    /// <summary>
    /// قیمت فی (واحد) فروش
    /// </summary>
    public decimal UnitPrice { private set; get; }

    /// <summary>
    /// جمع کل این ردیف (تعداد * قیمت واحد)
    /// </summary>
    public decimal TotalPrice => Quantity * UnitPrice;

    // سازنده مخصوص EF Core
    private SaleItem() { }

    internal SaleItem(Guid productId, int quantity, decimal unitPrice)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("شناسه محصول نامعتبر است.", nameof(productId));

        SetQuantity(quantity);
        SetUnitPrice(unitPrice);

        ProductId = productId;
    }

    internal void Update(int quantity, decimal unitPrice)
    {
        SetQuantity(quantity);
        SetUnitPrice(unitPrice);
    }

    private void SetQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("تعداد فروخته‌شده باید بزرگتر از صفر باشد.", nameof(quantity));

        Quantity = quantity;
    }

    private void SetUnitPrice(decimal unitPrice)
    {
        if (unitPrice < 0)
            throw new ArgumentException("قیمت واحد فروش نمی‌تواند منفی باشد.", nameof(unitPrice));

        UnitPrice = unitPrice;
    }
}

