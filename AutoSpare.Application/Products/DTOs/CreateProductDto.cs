namespace AutoSpare.Application.Products.DTOs;

public class CreateProductDto
{
    public string Name { get; set; } = string.Empty;
    public string InternalCode { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }

    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }

    // فیلدهای مربوط به انبار و موجودی اولیه
    public Guid? DefaultWarehouseId { get; set; }
    public int InitialQuantity { get; set; } = 0;
}

public record DropdownItemDto(Guid Id, string Title);

