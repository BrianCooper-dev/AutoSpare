using AutoSpare.Domain.Brands;
using AutoSpare.Domain.Categories;
using AutoSpare.Domain.Common;
using AutoSpare.Domain.Inventories;
using AutoSpare.Domain.Products.Enums;
using AutoSpare.Domain.Warehouses;

namespace AutoSpare.Domain.Products;

public class Product : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string InternalCode { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string? ImagePath { get; private set; }
    public decimal PurchasePrice { get; private set; }
    public decimal SalePrice { get; private set; }
    public ProductStatus Status { get; private set; } = ProductStatus.Active;

    // ارتباط با دسته‌بندی
    public Guid CategoryId { get; private set; }
    public Category? Category { get; private set; }

    // ارتباط با برند
    public Guid BrandId { get; private set; }
    public Brand? Brand { get; private set; }

    // ارتباط با انبار پیش‌فرض (اختیاری)
    public Guid? DefaultWarehouseId { get; private set; }
    public Warehouse? DefaultWarehouse { get; private set; }

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
        Guid categoryId,
        Guid brandId,
        string? imagePath = null,
        Guid? defaultWarehouseId = null)
    {
        SetBasicInformation(name, internalCode, model);
        SetPrices(purchasePrice, salePrice);
        SetCategory(categoryId);
        SetBrand(brandId);
        SetDefaultWarehouse(defaultWarehouseId);

        ImagePath = NormalizeOptional(imagePath);
        Status = ProductStatus.Active;
    }

    public void UpdateDetails(
        string name,
        string internalCode,
        string model,
        Guid categoryId,
        Guid brandId,
        string? imagePath = null)
    {
        SetBasicInformation(name, internalCode, model);
        SetCategory(categoryId);
        SetBrand(brandId);
        ImagePath = NormalizeOptional(imagePath);
        UpdateModificationTime();
    }

    public void UpdatePrices(decimal purchasePrice, decimal salePrice)
    {
        SetPrices(purchasePrice, salePrice);
        UpdateModificationTime();
    }

    public void SetDefaultWarehouse(Guid? warehouseId)
    {
        if (warehouseId.HasValue && warehouseId.Value == Guid.Empty)
        {
            throw new ArgumentException("شناسه انبار نامعتبر است.", nameof(warehouseId));
        }

        DefaultWarehouseId = warehouseId;
        UpdateModificationTime();
    }

    public void AssignCategory(Guid categoryId)
    {
        SetCategory(categoryId);
        UpdateModificationTime();
    }

    public void AssignBrand(Guid brandId)
    {
        SetBrand(brandId);
        UpdateModificationTime();
    }

    public void ChangeStatus(ProductStatus newStatus)
    {
        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(newStatus), "وضعیت کالا معتبر نیست.");
        }

        Status = newStatus;
        UpdateModificationTime();
    }

    private void SetBasicInformation(string name, string internalCode, string model)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("نام محصول نمی‌تواند خالی باشد.", nameof(name));

        if (string.IsNullOrWhiteSpace(internalCode))
            throw new ArgumentException("کد داخلی محصول نمی‌تواند خالی باشد.", nameof(internalCode));

        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("مدل محصول نمی‌تواند خالی باشد.", nameof(model));

        Name = name.Trim();
        InternalCode = internalCode.Trim();
        Model = model.Trim();
    }

    private void SetCategory(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
            throw new ArgumentException("شناسه دسته‌بندی نامعتبر است.", nameof(categoryId));

        CategoryId = categoryId;
    }

    private void SetBrand(Guid brandId)
    {
        if (brandId == Guid.Empty)
            throw new ArgumentException("شناسه برند نامعتبر است.", nameof(brandId));

        BrandId = brandId;
    }

    private void SetPrices(decimal purchasePrice, decimal salePrice)
    {
        if (purchasePrice < 0)
            throw new ArgumentOutOfRangeException(nameof(purchasePrice), "قیمت خرید نمی‌تواند منفی باشد.");

        if (salePrice < 0)
            throw new ArgumentOutOfRangeException(nameof(salePrice), "قیمت فروش نمی‌تواند منفی باشد.");

        if (salePrice < purchasePrice)
            throw new ArgumentException("قیمت فروش نمی‌تواند کمتر از قیمت خرید باشد.");

        PurchasePrice = purchasePrice;
        SalePrice = salePrice;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private readonly List<Inventory> _inventories = new();
    public virtual IReadOnlyCollection<Inventory> Inventories => _inventories.AsReadOnly();
}
