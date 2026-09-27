namespace AutoSpare.Application.Categories.DTOs;

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public int ProductsCount { get; set; }
}

public class CreateCategoryModel
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateCategoryModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
