using AutoSpare.Domain.Products.Enums;

namespace AutoSpare.Application.Products.DTOs;

public class PriceChangeHistoryDto
{
    public Guid Id { get; set; }
    public decimal OldPurchasePrice { get; set; }
    public decimal NewPurchasePrice { get; set; }
    public decimal OldSalePrice { get; set; }
    public decimal NewSalePrice { get; set; }
    public PriceChangeType ChangeType { get; set; }
    public DateTime ChangeDate { get; set; }
    public string? Reason { get; set; }

    // فیلدهای محاسباتی کمکی برای نمایش در UI
    public decimal SalePriceDifference => NewSalePrice - OldSalePrice;
    public decimal SalePricePercentageChange =>
        OldSalePrice > 0 ? Math.Round(((NewSalePrice - OldSalePrice) / OldSalePrice) * 100, 2) : 0;
}
