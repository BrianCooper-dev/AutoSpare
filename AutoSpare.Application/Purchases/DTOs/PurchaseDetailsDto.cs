using AutoSpare.Domain.Purchases.Enums;

namespace AutoSpare.Application.Purchases.DTOs;

public class PurchaseDetailsDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public PurchaseStatus Status { get; set; }
    public string? Notes { get; set; }
    public decimal TotalAmount { get; set; }

    public List<PurchaseItemDetailsDto> Items { get; set; } = new();
}

public class PurchaseItemDetailsDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string InternalCode { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string? Model { get; set; }
    public string? ImagePath { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

