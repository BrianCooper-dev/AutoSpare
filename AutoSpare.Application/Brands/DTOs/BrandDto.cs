namespace AutoSpare.Application.Brands.DTOs;

public class BrandDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int ProductsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBrandModel
{
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Description { get; set; }
}

public class UpdateBrandModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Description { get; set; }
}
