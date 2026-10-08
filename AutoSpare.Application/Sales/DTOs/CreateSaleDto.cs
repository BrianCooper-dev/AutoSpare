using System.ComponentModel.DataAnnotations;

namespace AutoSpare.Application.Sales.DTOs;

public class CreateSaleDto
{
    [Required(ErrorMessage = "شماره فاکتور فروش الزامی است.")]
    public string InvoiceNumber { get; set; } = string.Empty;

    public string? CustomerName { get; set; } = "مشتری نقدی";

    [Required(ErrorMessage = "انتخاب انبار مبدأ الزامی است.")]
    public Guid? WarehouseId { get; set; }

    public DateTime? SaleDate { get; set; } = DateTime.Today;

    public string? Notes { get; set; }

    public bool FinalizeImmediately { get; set; } = true;

    public List<CreateSaleItemDto> Items { get; set; } = new();
}
