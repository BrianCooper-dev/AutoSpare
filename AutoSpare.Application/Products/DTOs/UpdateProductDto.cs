using System.ComponentModel.DataAnnotations;

namespace AutoSpare.Application.Products.DTOs;

public class UpdateProductDto
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "نام کالا الزامی است.")]
    [MaxLength(
        200,
        ErrorMessage = "نام کالا نمی‌تواند بیش از ۲۰۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "کد فنی الزامی است.")]
    [MaxLength(
        50,
        ErrorMessage = "کد فنی نمی‌تواند بیش از ۵۰ کاراکتر باشد.")]
    public string InternalCode { get; set; } = string.Empty;

    [MaxLength(
        150,
        ErrorMessage = "مدل نمی‌تواند بیش از ۱۵۰ کاراکتر باشد.")]
    public string? Model { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "79228162514264337593543950335",
        ErrorMessage = "قیمت خرید نمی‌تواند منفی باشد.")]
    public decimal PurchasePrice { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "79228162514264337593543950335",
        ErrorMessage = "قیمت فروش نمی‌تواند منفی باشد.")]
    public decimal SalePrice { get; set; }

    [Required(ErrorMessage = "انتخاب دسته‌بندی الزامی است.")]
    public Guid? CategoryId { get; set; }

    [Required(ErrorMessage = "انتخاب برند الزامی است.")]
    public Guid? BrandId { get; set; }

    public Guid? DefaultWarehouseId { get; set; }

    [MaxLength(
        500,
        ErrorMessage = "مسیر تصویر نمی‌تواند بیش از ۵۰۰ کاراکتر باشد.")]
    public string? ImagePath { get; set; }

    public bool IsLowStockAlertEnabled { get; set; }

    [Range(
        0,
        int.MaxValue,
        ErrorMessage = "آستانه هشدار موجودی نمی‌تواند منفی باشد.")]
    public int MinStockThreshold { get; set; }
}
