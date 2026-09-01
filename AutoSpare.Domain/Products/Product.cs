using AutoSpare.Domain.Common;
using AutoSpare.Domain.Products.Enums;

namespace AutoSpare.Domain.Products;

public class Product : BaseEntity
{
    public string Name { get; private set; } = string.Empty;

    public string InternalCode { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public string? ImagePath { get; private set; }

    public decimal PurchasePrice { get; private set; }

    public decimal SalePrice { get; private set; }

    public Guid? DefaultWarehouseId { get; private set; }

    public ProductStatus Status { get; private set; } = ProductStatus.Active;

    // سازنده خالی برای EF Core
    protected Product()
    {
    }

    public Product(
        string name,
        string internalCode,
        string model,
        decimal purchasePrice,
        decimal salePrice,
        string? imagePath = null,
        Guid? defaultWarehouseId = null)
    {
        SetBasicInformation(name, internalCode, model);
        SetPrices(purchasePrice, salePrice);

        ImagePath = NormalizeOptional(imagePath);
        DefaultWarehouseId = defaultWarehouseId;
        Status = ProductStatus.Active;
    }

    public void UpdateDetails(
        string name,
        string internalCode,
        string model,
        string? imagePath = null)
    {
        SetBasicInformation(name, internalCode, model);
        ImagePath = NormalizeOptional(imagePath);
        UpdateModificationTime();
    }

    public void UpdatePrices(
        decimal purchasePrice,
        decimal salePrice)
    {
        SetPrices(purchasePrice, salePrice);
        UpdateModificationTime();
    }

    public void SetDefaultWarehouse(Guid? warehouseId)
    {
        DefaultWarehouseId = warehouseId;
        UpdateModificationTime();
    }

    public void ChangeStatus(ProductStatus newStatus)
    {
        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(newStatus),
                "وضعیت کالا معتبر نیست.");
        }

        Status = newStatus;
        UpdateModificationTime();
    }

    private void SetBasicInformation(
        string name,
        string internalCode,
        string model)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "نام محصول نمی‌تواند خالی باشد.",
                nameof(name));
        }

        if (string.IsNullOrWhiteSpace(internalCode))
        {
            throw new ArgumentException(
                "کد داخلی محصول نمی‌تواند خالی باشد.",
                nameof(internalCode));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException(
                "مدل محصول نمی‌تواند خالی باشد.",
                nameof(model));
        }

        Name = name.Trim();
        InternalCode = internalCode.Trim();
        Model = model.Trim();
    }

    private void SetPrices(
        decimal purchasePrice,
        decimal salePrice)
    {
        if (purchasePrice < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(purchasePrice),
                "قیمت خرید نمی‌تواند منفی باشد.");
        }

        if (salePrice < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(salePrice),
                "قیمت فروش نمی‌تواند منفی باشد.");
        }

        if (salePrice < purchasePrice)
        {
            throw new ArgumentException(
                "قیمت فروش نمی‌تواند کمتر از قیمت خرید باشد.");
        }

        PurchasePrice = purchasePrice;
        SalePrice = salePrice;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
