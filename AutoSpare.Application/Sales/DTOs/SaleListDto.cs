using AutoSpare.Domain.Sales.Enums;

namespace AutoSpare.Application.Sales.DTOs;

public class SaleListDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? WarehouseName { get; set; }
    public DateTime SaleDate { get; set; }
    public SaleStatus Status { get; set; }
    public int ItemsCount { get; set; }
    public decimal TotalAmount { get; set; }
}
