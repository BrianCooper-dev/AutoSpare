using AutoSpare.Domain.Purchases.Enums;

namespace AutoSpare.Application.Purchases.DTOs;

public class PurchaseListDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public PurchaseStatus Status { get; set; }
    public int ItemsCount { get; set; }
    public decimal TotalAmount { get; set; }
}

