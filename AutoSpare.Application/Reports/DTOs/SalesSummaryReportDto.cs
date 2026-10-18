namespace AutoSpare.Application.Reports.DTOs;

public class SalesSummaryReportDto
{
    public DateTime Date { get; set; }
    public int InvoiceCount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
}
