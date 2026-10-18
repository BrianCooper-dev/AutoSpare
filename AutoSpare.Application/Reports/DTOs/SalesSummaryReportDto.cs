namespace AutoSpare.Application.Reports.DTOs;

public class SalesSummaryReportDto
{
    public DateTime Date { get; set; }
    public string PeriodTitle { get; set; } = string.Empty;
    public int InvoiceCount { get; set; }
    public int TotalQuantitySold { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
}
