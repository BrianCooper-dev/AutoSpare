using AutoSpare.Application.Products.DTOs;
using AutoSpare.Application.Purchases;
using AutoSpare.Application.Purchases.DTOs;
using AutoSpare.Domain.Inventories;
using AutoSpare.Domain.Inventories.Enums;
using AutoSpare.Domain.Purchases;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Infrastructure.Services;

public class PurchaseService : IPurchaseService
{
    private readonly ApplicationDbContext _context;

    public PurchaseService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DropdownItemDto>> GetSuppliersLookupAsync() =>
        await _context.Suppliers
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new DropdownItemDto(s.Id, s.Name))
            .ToListAsync();

    public async Task<List<DropdownItemDto>> GetWarehousesLookupAsync() =>
        await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.IsActive)
            .OrderBy(w => w.Name)
            .Select(w => new DropdownItemDto(w.Id, w.Name))
            .ToListAsync();

    public async Task<List<ProductLookupItemDto>> GetProductsLookupAsync() =>
        await _context.Products
            .AsNoTracking()
            .Where(p => p.Status == Domain.Products.Enums.ProductStatus.Active)
            .OrderBy(p => p.Name)
            .Select(p => new ProductLookupItemDto(
                p.Id,
                p.Name,
                p.InternalCode,
                p.Model,
                p.Brand != null ? p.Brand.Name : null,
                p.PurchasePrice,
                p.ImagePath,
                p.DefaultWarehouseId
            ))
            .ToListAsync();

    public async Task<Guid> CreatePurchaseAsync(CreatePurchaseDto dto, string? currentUserName = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.InvoiceNumber))
            throw new ArgumentException("شماره فاکتور خرید الزامی است.");

        if (!dto.SupplierId.HasValue || dto.SupplierId.Value == Guid.Empty)
            throw new ArgumentException("انتخاب تأمین‌کننده الزامی است.");

        if (!dto.WarehouseId.HasValue || dto.WarehouseId.Value == Guid.Empty)
            throw new ArgumentException("انتخاب انبار مقصد الزامی است.");

        if (dto.Items == null || dto.Items.Count == 0)
            throw new ArgumentException("ثبت حداقل یک قلم کالا برای فاکتور خرید الزامی است.");

        var validItems = dto.Items
            .Where(i => i.ProductId.HasValue && i.ProductId.Value != Guid.Empty)
            .ToList();

        if (validItems.Count == 0)
            throw new ArgumentException("حداقل یک ردیف کالای معتبر باید در فاکتور وجود داشته باشد.");

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var invoiceTrimmed = dto.InvoiceNumber.Trim();

                var isDuplicateInvoice = await _context.Purchases
                    .AnyAsync(p => p.InvoiceNumber == invoiceTrimmed);

                if (isDuplicateInvoice)
                    throw new InvalidOperationException($"فاکتور خرید با شماره '{invoiceTrimmed}' قبلاً ثبت شده است.");

                var purchase = new Purchase(
                    invoiceNumber: invoiceTrimmed,
                    supplierId: dto.SupplierId.Value,
                    warehouseId: dto.WarehouseId.Value,
                    purchaseDate: dto.PurchaseDate.HasValue
                        ? DateTime.SpecifyKind(dto.PurchaseDate.Value, DateTimeKind.Utc)
                        : DateTime.UtcNow,
                    notes: dto.Notes);

                // نگه داشتن نسخه‌ی معتبر اقلام برای تصمیم‌گیری‌های بعدی
                var normalizedItems = new List<CreatePurchaseItemDto>();

                foreach (var item in validItems)
                {
                    if (item.Quantity <= 0)
                        throw new ArgumentException("تعداد کالا باید بزرگتر از صفر باشد.");

                    if (item.UnitPrice.HasValue && item.UnitPrice.Value < 0)
                        throw new ArgumentException("قیمت واحد نمی‌تواند منفی باشد.");

                    var normalizedUnitPrice = item.UnitPrice ?? 0m;

                    purchase.AddOrUpdateItem(
                        item.ProductId!.Value,
                        item.Quantity,
                        normalizedUnitPrice);

                    normalizedItems.Add(new CreatePurchaseItemDto
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    });
                }

                await _context.Purchases.AddAsync(purchase);

                if (dto.FinalizeImmediately)
                {
                    purchase.Complete();

                    // فقط برای آپدیت قیمت از DTO استفاده می‌کنیم
                    foreach (var dtoItem in normalizedItems)
                    {
                        if (!dtoItem.ProductId.HasValue)
                            continue;

                        if (dtoItem.UnitPrice.HasValue && dtoItem.UnitPrice.Value > 0)
                        {
                            var product = await _context.Products.FindAsync(dtoItem.ProductId.Value);
                            if (product != null)
                            {
                                var newSalePrice = Math.Max(product.SalePrice, dtoItem.UnitPrice.Value);
                                product.UpdatePrices(dtoItem.UnitPrice.Value, newSalePrice);
                            }
                        }
                    }

                    // برای افزایش موجودی از purchase.Items استفاده می‌کنیم
                    foreach (var item in purchase.Items)
                    {
                        var inventory = _context.Inventories.Local
                            .FirstOrDefault(i =>
                                i.ProductId == item.ProductId &&
                                i.WarehouseId == purchase.WarehouseId);

                        if (inventory == null)
                        {
                            inventory = await _context.Inventories
                                .Include(i => i.Movements)
                                .FirstOrDefaultAsync(i =>
                                    i.ProductId == item.ProductId &&
                                    i.WarehouseId == purchase.WarehouseId);
                        }

                        if (inventory == null)
                        {
                            inventory = new Inventory(item.ProductId, purchase.WarehouseId, initialQuantity: 0);
                            await _context.Inventories.AddAsync(inventory);
                        }

                        inventory.IncreaseQuantity(
                            amount: item.Quantity,
                            type: StockMovementType.Purchase,
                            reason: $"خرید طی فاکتور {purchase.InvoiceNumber}",
                            reference: purchase.InvoiceNumber,
                            performedBy: currentUserName ?? "سیستم");

                        foreach (var movement in inventory.Movements)
                        {
                            var movementEntry = _context.Entry(movement);
                            if (movementEntry.State == EntityState.Detached ||
                                movementEntry.State == EntityState.Modified)
                            {
                                movementEntry.State = EntityState.Added;
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return purchase.Id;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"[TRANSACTION ROLLBACK] خطا در ثبت تراکنشی خرید: {ex.Message}");
                throw;
            }
        });
    }

    public async Task<List<PurchaseListDto>> GetPurchasesAsync()
    {
        return await _context.Purchases
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.Items)
            .OrderByDescending(p => p.PurchaseDate)
            .Select(p => new PurchaseListDto
            {
                Id = p.Id,
                InvoiceNumber = p.InvoiceNumber,
                SupplierName = p.Supplier != null ? p.Supplier.Name : "-",
                WarehouseName = p.Warehouse != null ? p.Warehouse.Name : "-",
                PurchaseDate = p.PurchaseDate,
                Status = p.Status,
                ItemsCount = p.Items.Count,
                TotalAmount = p.TotalAmount
            })
            .ToListAsync();
    }

    public async Task<string> GenerateInvoiceNumberAsync()
    {
        int maxNumber = 0;

        var allInvoiceNumbers = await _context.Purchases
            .AsNoTracking()
            .Select(p => p.InvoiceNumber)
            .ToListAsync();

        foreach (var inv in allInvoiceNumbers)
        {
            if (!string.IsNullOrWhiteSpace(inv) && inv.StartsWith("PUR-"))
            {
                var numPart = inv.Replace("PUR-", "");
                if (int.TryParse(numPart, out var parsedNum) && parsedNum > maxNumber)
                {
                    maxNumber = parsedNum;
                }
            }
        }

        var nextNumber = maxNumber + 1;
        var candidate = $"PUR-{nextNumber:D5}";

        while (await _context.Purchases.AnyAsync(p => p.InvoiceNumber == candidate))
        {
            nextNumber++;
            candidate = $"PUR-{nextNumber:D5}";
        }

        return candidate;
    }
}
