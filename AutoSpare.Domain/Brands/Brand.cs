using AutoSpare.Domain.Common;
using AutoSpare.Domain.Products;

namespace AutoSpare.Domain.Brands;

public class Brand : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Country { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    // ارتباط ناوبری با محصولات
    private readonly List<Product> _products = [];
    public IReadOnlyCollection<Product> Products => _products.AsReadOnly();

    // سازنده خالی برای EF Core
    protected Brand()
    {
    }

    public Brand(string name, string? country = null, string? description = null)
    {
        SetName(name);
        Country = NormalizeOptional(country);
        Description = NormalizeOptional(description);
        IsActive = true;
    }

    public void UpdateDetails(string name, string? country = null, string? description = null)
    {
        SetName(name);
        Country = NormalizeOptional(country);
        Description = NormalizeOptional(description);
        UpdateModificationTime();
    }

    public void Activate()
    {
        if (!IsActive)
        {
            IsActive = true;
            UpdateModificationTime();
        }
    }

    public void Deactivate()
    {
        if (IsActive)
        {
            IsActive = false;
            UpdateModificationTime();
        }
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("نام برند نمی‌تواند خالی باشد.", nameof(name));
        }

        Name = name.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

