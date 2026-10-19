namespace AutoSpare.Application.Reports.DTOs;

public class ProfitItemReportDto
{
    public Guid SaleId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public int Quantity { get; set; }

    /// <summary>قیمت واحد خرید کالا در لحظه فروش (Snapshot)</summary>
    public decimal UnitCost { get; set; }

    /// <summary>قیمت واحد فروش در لحظه ثبت (Snapshot)</summary>
    public decimal UnitPrice { get; set; }

    public decimal TotalSales => Quantity * UnitPrice;
    public decimal TotalCost => Quantity * UnitCost;
    public decimal Profit => TotalSales - TotalCost;

    public double ProfitMarginPercentage =>
        TotalSales > 0 ? (double)(Profit / TotalSales) * 100 : 0;
}

