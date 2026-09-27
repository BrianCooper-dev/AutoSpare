using AutoSpare.Application.Categories.DTOs;

namespace AutoSpare.Application.Categories;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync(string? searchTerm = null, bool? activeOnly = null,
        CancellationToken cancellationToken = default);

    Task<CategoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(bool Success, string? ErrorMessage, Guid? Id)> CreateAsync(CreateCategoryModel model,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? ErrorMessage)> UpdateAsync(UpdateCategoryModel model,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? ErrorMessage)>
        ToggleStatusAsync(Guid id, CancellationToken cancellationToken = default);
}
