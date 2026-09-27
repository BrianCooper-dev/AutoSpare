using AutoSpare.Application.Suppliers.DTOs;

namespace AutoSpare.Application.Suppliers;

public interface ISupplierService
{
    Task<List<SupplierDto>> GetAllAsync(string? searchTerm = null, bool? isActive = null);
    Task<SupplierDto?> GetByIdAsync(Guid id);

    Task<Guid> CreateAsync(CreateSupplierModel model);
    Task UpdateAsync(UpdateSupplierModel model);

    Task<bool> ToggleStatusAsync(Guid id);
}

