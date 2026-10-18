namespace AutoSpare.Application.Reports.DTOs;

public class TopSellingProductDto
{
    public Guid ProductId { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public int TotalQuantitySold { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public decimal TotalProfitAmount { get; set; }
}
