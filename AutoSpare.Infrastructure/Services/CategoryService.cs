using AutoSpare.Application.Categories;
using AutoSpare.Application.Categories.DTOs;
using AutoSpare.Domain.Categories;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _dbContext;

    public CategoryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CategoryDto>> GetAllAsync(string? searchTerm = null, bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Categories
            .AsNoTracking()
            .AsQueryable();

        if (activeOnly.HasValue)
        {
            query = query.Where(c => c.IsActive == activeOnly.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(search) ||
                (c.Description != null && c.Description.ToLower().Contains(search)));
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                LastModifiedAt = c.LastModifiedAt,
                ProductsCount = c.Products.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                LastModifiedAt = c.LastModifiedAt,
                ProductsCount = c.Products.Count
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(bool Success, string? ErrorMessage, Guid? Id)> CreateAsync(CreateCategoryModel model,
        CancellationToken cancellationToken = default)
    {
        var trimmedName = model.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return (false, "نام دسته‌بندی الزامی است.", null);
        }

        var isDuplicate = await _dbContext.Categories
            .AnyAsync(c => c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (isDuplicate)
        {
            return (false, "دسته‌بندی با این نام از قبل وجود دارد.", null);
        }

        try
        {
            var category = new Category(trimmedName, model.Description);
            await _dbContext.Categories.AddAsync(category, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return (true, null, category.Id);
        }
        catch (Exception ex)
        {
            return (false, $"خطا در ثبت دسته‌بندی: {ex.Message}", null);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(UpdateCategoryModel model,
        CancellationToken cancellationToken = default)
    {
        var trimmedName = model.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return (false, "نام دسته‌بندی الزامی است.");
        }

        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == model.Id, cancellationToken);

        if (category is null)
        {
            return (false, "دسته‌بندی مورد نظر یافت نشد.");
        }

        var isDuplicate = await _dbContext.Categories
            .AnyAsync(c => c.Id != model.Id && c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (isDuplicate)
        {
            return (false, "دسته‌بندی دیگری با این نام وجود دارد.");
        }

        try
        {
            category.UpdateDetails(trimmedName, model.Description);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"خطا در ویرایش دسته‌بندی: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> ToggleStatusAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
        {
            return (false, "دسته‌بندی مورد نظر یافت نشد.");
        }

        try
        {
            if (category.IsActive)
            {
                category.Deactivate();
            }
            else
            {
                category.Activate();
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"خطا در تغییر وضعیت: {ex.Message}");
        }
    }
}
