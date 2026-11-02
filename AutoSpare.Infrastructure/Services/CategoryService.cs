using AutoSpare.Application.Categories;
using AutoSpare.Application.Categories.DTOs;
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
            var search = searchTerm.Trim();
            // اجتناب از ToLower روی فیلد پایگاه داده جهت استفاده بهینه از Index
            query = query.Where(c =>
                c.Name.Contains(search) ||
                (c.Description != null && c.Description.Contains(search)));
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
            .AnyAsync(c => c.Name == trimmedName, cancellationToken);

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
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "خطای پایگاه داده در ثبت دسته‌بندی با نام {CategoryName}", trimmedName);
            return (false, "دسته‌بندی با این نام یا مشخصات قبلاً ثبت شده است.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطای سیستمی در ثبت دسته‌بندی با نام {CategoryName}", trimmedName);
            return (false, "خطای غیرمنتظره در ثبت دسته‌بندی.", null);
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
            .AnyAsync(c => c.Id != model.Id && c.Name == trimmedName, cancellationToken);

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
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "خطای همزمانی یا یکتایی در ویرایش دسته‌بندی {CategoryId}", model.Id);
            return (false, "دسته‌بندی دیگری با این نام وجود دارد.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در ویرایش دسته‌بندی با شناسه {CategoryId}", model.Id);
            return (false, "خطای سیستمی در ویرایش دسته‌بندی.");
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
            _logger.LogError(ex, "خطا در تغییر وضعیت دسته‌بندی با شناسه {CategoryId}", id);
            return (false, "خطای سیستمی در تغییر وضعیت دسته‌بندی.");
        }
    }
}
