using AutoSpare.Application.Brands.DTOs;

namespace AutoSpare.Application.Brands;

public interface IBrandService
{
    Task<List<BrandDto>> GetAllAsync(string? searchTerm = null, bool? isActive = null);
    Task<BrandDto?> GetByIdAsync(Guid id);
    Task<(bool Success, string? ErrorMessage, Guid? Id)> CreateAsync(CreateBrandModel model);
    Task<(bool Success, string? ErrorMessage)> UpdateAsync(UpdateBrandModel model);
    Task<(bool Success, string? ErrorMessage)> ToggleStatusAsync(Guid id);
}

