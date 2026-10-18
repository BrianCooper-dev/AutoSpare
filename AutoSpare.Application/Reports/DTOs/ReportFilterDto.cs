namespace AutoSpare.Application.Reports.DTOs;

public class ReportFilterDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? WarehouseId { get; set; }
}

