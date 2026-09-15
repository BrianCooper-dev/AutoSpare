using AutoSpare.Domain.Common;
using AutoSpare.Domain.Products;

namespace AutoSpare.Domain.Purchases;

/// <summary>
/// ردیف کالای خریداری‌شده در فاکتور خرید
/// </summary>
public class PurchaseItem : BaseEntity
{
    public Guid PurchaseId { private set; get; }

    public Guid ProductId { private set; get; }
    public Product? Product { private set; get; }

    /// <summary>
    /// تعداد خریداری شده
    /// </summary>
    public int Quantity { private set; get; }

    /// <summary>
    /// قیمت فی (واحد) خرید
    /// </summary>
    public decimal UnitPrice { private set; get; }

    /// <summary>
    /// جمع کل این ردیف (تعداد * قیمت واحد)
    /// </summary>
    public decimal TotalPrice => Quantity * UnitPrice;

    // سازنده مخصوص EF Core
    private PurchaseItem() { }

    internal PurchaseItem(Guid productId, int quantity, decimal unitPrice)
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
            throw new ArgumentException("تعداد خریداری‌شده باید بزرگتر از صفر باشد.", nameof(quantity));

        Quantity = quantity;
    }

    private void SetUnitPrice(decimal unitPrice)
    {
        if (unitPrice < 0)
            throw new ArgumentException("قیمت واحد نمی‌تواند منفی باشد.", nameof(unitPrice));

        UnitPrice = unitPrice;
    }
}

