using AutoSpare.Application.Brands.DTOs;
using AutoSpare.Application.Brands;
using AutoSpare.Domain.Brands;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoSpare.Infrastructure.Services;

public class BrandService : IBrandService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BrandService> _logger;

    public BrandService(ApplicationDbContext context, ILogger<BrandService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<BrandDto>> GetAllAsync(string? searchTerm = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Brands
            .AsNoTracking()
            .AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(b => b.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(b =>
                b.Name.Contains(term) ||
                (b.Country != null && b.Country.Contains(term)) ||
                (b.Description != null && b.Description.Contains(term)));
        }

        return await query
            .OrderBy(b => b.Name)
            .Select(b => new BrandDto
            {
                Id = b.Id,
                Name = b.Name,
                Country = b.Country,
                Description = b.Description,
                IsActive = b.IsActive,
                ProductsCount = b.Products.Count,
                CreatedAt = b.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<BrandDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Brands
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new BrandDto
            {
                Id = b.Id,
                Name = b.Name,
                Country = b.Country,
                Description = b.Description,
                IsActive = b.IsActive,
                ProductsCount = b.Products.Count,
                CreatedAt = b.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(bool Success, string? ErrorMessage, Guid? Id)> CreateAsync(CreateBrandModel model, CancellationToken cancellationToken = default)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Name))
        {
            return (false, "نام برند الزامی است.", null);
        }

        var trimmedName = model.Name.Trim();

        try
        {
            var brand = new Brand(trimmedName, model.Country?.Trim(), model.Description?.Trim());
            await _context.Brands.AddAsync(brand, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return (true, null, brand.Id);
        }
        catch (DbUpdateException)
        {
            return (false, "برندی با این نام از قبل وجود دارد.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در ایجاد برند جدید");
            return (false, "خطای سیستمی هنگام ثبت برند رخ داد.", null);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(UpdateBrandModel model, CancellationToken cancellationToken = default)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Name))
        {
            return (false, "نام برند الزامی است.");
        }

        var brand = await _context.Brands.FirstOrDefaultAsync(b => b.Id == model.Id, cancellationToken);
        if (brand == null)
        {
            return (false, "برند مورد نظر یافت نشد.");
        }

        var trimmedName = model.Name.Trim();

        try
        {
            brand.UpdateDetails(trimmedName, model.Country?.Trim(), model.Description?.Trim());
            await _context.SaveChangesAsync(cancellationToken);

            return (true, null);
        }
        catch (DbUpdateException)
        {
            return (false, "برند دیگری با این نام از قبل وجود دارد.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در به‌روزرسانی برند {BrandId}", model.Id);
            return (false, "خطای سیستمی هنگام ویرایش برند رخ داد.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> ToggleStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var brand = await _context.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (brand == null)
        {
            return (false, "برند مورد نظر یافت نشد.");
        }

        try
        {
            if (brand.IsActive)
            {
                brand.Deactivate();
            }
            else
            {
                brand.Activate();
            }

            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در تغییر وضعیت برند {BrandId}", id);
            return (false, "خطای سیستمی هنگام تغییر وضعیت برند رخ داد.");
        }
    }
}
