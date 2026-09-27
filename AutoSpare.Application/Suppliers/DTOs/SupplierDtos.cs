namespace AutoSpare.Application.Suppliers.DTOs;

public record SupplierDto(
    Guid Id,
    string Name,
    string? ContactPerson,
    string? Phone,
    string Mobile,
    string? Email,
    string? Address,
    string? EconomicCode,
    decimal CreditLimit,
    bool IsActive,
    DateTime CreatedAt
);

public class CreateSupplierModel
{
    public string Name { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;

    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? EconomicCode { get; set; }
    public decimal CreditLimit { get; set; } = 0;
}

public class UpdateSupplierModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;

    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? EconomicCode { get; set; }
    public decimal CreditLimit { get; set; } = 0;
}
