using AutoSpare.Domain.Common;

namespace AutoSpare.Domain.Warehouses;

public class Warehouse : BaseEntity
{
    // شناسه‌های ثابت برای Seed Data و استفاده در منطق برنامه
    public static readonly Guid ShopWarehouseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid HomeWarehouseId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public string Name { get; private set; } = string.Empty;
    public string? Code { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; } = true;

    // سازنده خالی برای EF Core
    protected Warehouse()
    {
    }

    public Warehouse(string name, string? code = null, string? address = null)
    {
        SetName(name);
        Code = NormalizeOptional(code);
        Address = NormalizeOptional(address);
        IsActive = true;
    }

    // سازنده ویژه برای رکوردهای اولیه با Id ثابت (Seed Data)
    public Warehouse(Guid id, string name, string? code = null, string? address = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("شناسه انبار نامعتبر است.", nameof(id));
        }

        Id = id;
        SetName(name);
        Code = NormalizeOptional(code);
        Address = NormalizeOptional(address);
        IsActive = true;
    }

    public void UpdateDetails(string name, string? code = null, string? address = null)
    {
        SetName(name);
        Code = NormalizeOptional(code);
        Address = NormalizeOptional(address);
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
            throw new ArgumentException("نام انبار نمی‌تواند خالی باشد.", nameof(name));
        }

        Name = name.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
