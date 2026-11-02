using AutoSpare.Application.Brands.DTOs;

namespace AutoSpare.Application.Brands;

public interface IBrandService
{
    Task<List<BrandDto>> GetAllAsync(
        string? searchTerm = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    Task<BrandDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? ErrorMessage, Guid? Id)> CreateAsync(
        CreateBrandModel model,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? ErrorMessage)> UpdateAsync(
        UpdateBrandModel model,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? ErrorMessage)> ToggleStatusAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
