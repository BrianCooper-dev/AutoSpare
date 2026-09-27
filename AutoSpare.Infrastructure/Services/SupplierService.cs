using AutoSpare.Application.Suppliers;
using AutoSpare.Application.Suppliers.DTOs;
using AutoSpare.Domain.Suppliers;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Infrastructure.Services;

public class SupplierService : ISupplierService
{
    private readonly ApplicationDbContext _db;

    public SupplierService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<SupplierDto>> GetAllAsync(string? searchTerm = null, bool? isActive = null)
    {
        var query = _db.Suppliers.AsNoTracking();

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x =>
                x.Name.Contains(term) ||
                (x.ContactPerson != null && x.ContactPerson.Contains(term)) ||
                x.Mobile.Contains(term) ||
                (x.Phone != null && x.Phone.Contains(term)) ||
                (x.EconomicCode != null && x.EconomicCode.Contains(term)));
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new SupplierDto(
                x.Id,
                x.Name,
                x.ContactPerson,
                x.Phone,
                x.Mobile,
                x.Email,
                x.Address,
                x.EconomicCode,
                x.CreditLimit,
                x.IsActive,
                x.CreatedAt
            ))
            .ToListAsync();
    }

    public async Task<SupplierDto?> GetByIdAsync(Guid id)
    {
        var x = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (x == null) return null;

        return new SupplierDto(
            x.Id,
            x.Name,
            x.ContactPerson,
            x.Phone,
            x.Mobile,
            x.Email,
            x.Address,
            x.EconomicCode,
            x.CreditLimit,
            x.IsActive,
            x.CreatedAt
        );
    }

    public async Task<Guid> CreateAsync(CreateSupplierModel model)
    {
        var supplier = new Supplier(
            name: model.Name,
            mobile: model.Mobile,
            contactPerson: model.ContactPerson,
            phone: model.Phone,
            email: model.Email,
            address: model.Address,
            economicCode: model.EconomicCode,
            creditLimit: model.CreditLimit
        );

        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        return supplier.Id;
    }

    public async Task UpdateAsync(UpdateSupplierModel model)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (supplier == null)
            throw new InvalidOperationException("تأمین‌کننده موردنظر یافت نشد.");

        supplier.UpdateDetails(
            name: model.Name,
            mobile: model.Mobile,
            contactPerson: model.ContactPerson,
            phone: model.Phone,
            email: model.Email,
            address: model.Address,
            economicCode: model.EconomicCode
        );

        supplier.SetCreditLimit(model.CreditLimit);

        await _db.SaveChangesAsync();
    }

    public async Task<bool> ToggleStatusAsync(Guid id)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id);
        if (supplier == null)
            throw new InvalidOperationException("تأمین‌کننده موردنظر یافت نشد.");

        if (supplier.IsActive) supplier.Deactivate();
        else supplier.Activate();

        await _db.SaveChangesAsync();
        return supplier.IsActive;
    }
}
