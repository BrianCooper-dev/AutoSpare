using AutoSpare.Application.Categories.DTOs;
using AutoSpare.Application.Categories;
using AutoSpare.Domain.Categories;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoSpare.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(ApplicationDbContext dbContext, ILogger<CategoryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<CategoryDto>> GetAllAsync(string? searchTerm = null, bool? activeOnly = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Categories
            .AsNoTracking()
            .AsQueryable();

        if (activeOnly.HasValue && activeOnly.Value)
        {
            query = query.Where(c => c.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(c =>
                c.Name.Contains(term) ||
                (c.Description != null && c.Description.Contains(term)));
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

    public async Task<(bool Success, string? ErrorMessage, Guid? Id)> CreateAsync(CreateCategoryModel model, CancellationToken cancellationToken = default)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Name))
        {
            return (false, "نام دسته‌بندی الزامی است.", null);
        }

        var trimmedName = model.Name.Trim();

        try
        {
            var category = new Category(trimmedName, model.Description?.Trim());
            await _dbContext.Categories.AddAsync(category, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return (true, null, category.Id);
        }
        catch (DbUpdateException)
        {
            return (false, "دسته‌بندی با این نام از قبل وجود دارد.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در ایجاد دسته‌بندی جدید");
            return (false, "خطای سیستمی هنگام ثبت دسته‌بندی رخ داد.", null);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(UpdateCategoryModel model, CancellationToken cancellationToken = default)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Name))
        {
            return (false, "نام دسته‌بندی الزامی است.");
        }

        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == model.Id, cancellationToken);
        if (category == null)
        {
            return (false, "دسته‌بندی مورد نظر یافت نشد.");
        }

        var trimmedName = model.Name.Trim();

        try
        {
            category.UpdateDetails(trimmedName, model.Description?.Trim());
            await _dbContext.SaveChangesAsync(cancellationToken);

            return (true, null);
        }
        catch (DbUpdateException)
        {
            return (false, "دسته‌بندی دیگری با این نام از قبل وجود دارد.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در به‌روزرسانی دسته‌بندی {CategoryId}", model.Id);
            return (false, "خطای سیستمی هنگام ویرایش دسته‌بندی رخ داد.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> ToggleStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category == null)
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
            _logger.LogError(ex, "خطا در تغییر وضعیت دسته‌بندی {CategoryId}", id);
            return (false, "خطای سیستمی هنگام تغییر وضعیت دسته‌بندی رخ داد.");
        }
    }
}
