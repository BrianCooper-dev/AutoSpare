using AutoSpare.Application.Brands;
using AutoSpare.Application.Brands.DTOs;
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

    public async Task<List<BrandDto>> GetAllAsync(string? searchTerm = null, bool? isActive = null)
    {
        var query = _context.Brands.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(b => b.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(b => b.Name.ToLower().Contains(term) ||
                                     (b.Country != null && b.Country.ToLower().Contains(term)) ||
                                     (b.Description != null && b.Description.ToLower().Contains(term)));
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
            .ToListAsync();
    }

    public async Task<BrandDto?> GetByIdAsync(Guid id)
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
            .FirstOrDefaultAsync();
    }

    public async Task<(bool Success, string? ErrorMessage, Guid? Id)> CreateAsync(CreateBrandModel model)
    {
        try
        {
            var trimmedName = model.Name.Trim();

            var exists = await _context.Brands.AnyAsync(b => b.Name.ToLower() == trimmedName.ToLower());
            if (exists)
            {
                return (false, "برندی با این نام قبلاً ثبت شده است.", null);
            }

            var brand = new Brand(trimmedName, model.Country, model.Description);

            await _context.Brands.AddAsync(brand);
            await _context.SaveChangesAsync();

            return (true, null, brand.Id);
        }
        catch (ArgumentException ex)
        {
            return (false, ex.Message, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در ثبت برند جدید");
            return (false, "خطای سیستمی در ثبت اطلاعات برند.", null);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(UpdateBrandModel model)
    {
        try
        {
            var brand = await _context.Brands.FindAsync(model.Id);
            if (brand == null)
            {
                return (false, "برند مورد نظر یافت نشد.");
            }

            var trimmedName = model.Name.Trim();

            var exists = await _context.Brands.AnyAsync(b => b.Name.ToLower() == trimmedName.ToLower() && b.Id != model.Id);
            if (exists)
            {
                return (false, "برند دیگری با این نام وجود دارد.");
            }

            brand.UpdateDetails(trimmedName, model.Country, model.Description);
            await _context.SaveChangesAsync();

            return (true, null);
        }
        catch (ArgumentException ex)
        {
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در ویرایش برند با شناسه {BrandId}", model.Id);
            return (false, "خطای سیستمی در ویرایش برند.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> ToggleStatusAsync(Guid id)
    {
        try
        {
            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
            {
                return (false, "برند مورد نظر یافت نشد.");
            }

            if (brand.IsActive)
            {
                brand.Deactivate();
            }
            else
            {
                brand.Activate();
            }

            await _context.SaveChangesAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در تغییر وضعیت برند با شناسه {BrandId}", id);
            return (false, "خطای سیستمی در تغییر وضعیت برند.");
        }
    }
}

