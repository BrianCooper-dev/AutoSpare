using AutoSpare.Domain.Products.Enums;

namespace AutoSpare.Application.Products.DTOs;

/// <summary>
/// قلم کالای انتخابی برای پیش‌نمایش و اعمال تغییر قیمت
/// </summary>
public class PriceAdjustmentItemDto
{
    public Guid ProductId { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string InternalCode { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; }
    public decimal CurrentSalePrice { get; set; }
    public decimal NewSalePrice { get; set; }
    public decimal Difference => NewSalePrice - CurrentSalePrice;
}


/// <summary>
/// مدل ارسال درخواست ویرایش گروهی قیمت
/// </summary>
public class BatchPriceAdjustmentDto
{
    public List<PriceAdjustmentItemUpdateDto> Items { get; set; } = new();
    public PriceChangeType ChangeType { get; set; }
    public string? Reason { get; set; }
}

public class PriceAdjustmentItemUpdateDto
{
    public Guid ProductId { get; set; }
    public decimal NewSalePrice { get; set; }
}

