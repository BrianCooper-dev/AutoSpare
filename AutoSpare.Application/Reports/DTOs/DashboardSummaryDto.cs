namespace AutoSpare.Application.Reports.DTOs;

public class DashboardSummaryDto
{
    // آمار فروش و سود
    public decimal TodaySalesAmount { get; set; }
    public decimal TodayProfitAmount { get; set; }
    public int TodayCompletedSalesCount { get; set; }

    public decimal MonthSalesAmount { get; set; }
    public decimal MonthProfitAmount { get; set; }
    public int MonthCompletedSalesCount { get; set; }

    // آمار خریدها
    public decimal TodayPurchasesAmount { get; set; }
    public decimal MonthPurchasesAmount { get; set; }

    // ارزش انبار
    public decimal TotalInventoryPurchaseValue { get; set; }
    public decimal TotalInventorySaleValue { get; set; }
    public int TotalStockItemsCount { get; set; }
}

