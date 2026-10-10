using AutoSpare.Application.Products.DTOs;
using AutoSpare.Application.Sales;
using AutoSpare.Application.Sales.DTOs;
using AutoSpare.Domain.Inventories.Enums;
using AutoSpare.Domain.Sales;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Infrastructure.Services;

public class SaleService : ISaleService
{
    private readonly ApplicationDbContext _context;

    public SaleService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DropdownItemDto>> GetWarehousesLookupAsync() =>
        await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.IsActive)
            .OrderBy(w => w.Name)
            .Select(w => new DropdownItemDto(w.Id, w.Name))
            .ToListAsync();

    public async Task<List<SaleProductLookupDto>> GetProductsForSaleLookupAsync()
    {
        var stocks = await _context.Inventories
            .AsNoTracking()
            .Select(i => new { i.ProductId, i.WarehouseId, i.Quantity })
            .ToListAsync();

        var stocksLookup = stocks
            .GroupBy(i => i.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(x => x.WarehouseId, x => x.Quantity));

        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.Status == Domain.Products.Enums.ProductStatus.Active)
            .Select(p => new
            {
                p.Id, p.Name, p.InternalCode, p.Model,
                BrandName = p.Brand != null ? p.Brand.Name : null,
                p.PurchasePrice, // ← این خط اضافه شد
                p.SalePrice, p.ImagePath
            })
            .OrderBy(p => p.Name)
            .ToListAsync();


        return products.Select(p =>
        {
            var perWarehouse = stocksLookup.TryGetValue(p.Id, out var s)
                ? s
                : new Dictionary<Guid, int>();

            var defaultWh = perWarehouse.Where(x => x.Value > 0)
                .OrderByDescending(x => x.Value)
                .Select(x => (Guid?)x.Key)
                .FirstOrDefault();

            return new SaleProductLookupDto(
                p.Id, p.Name, p.InternalCode, p.Model, p.BrandName, p.PurchasePrice, p.SalePrice, p.ImagePath,
                perWarehouse,
                defaultWh,
                perWarehouse.Values.Sum());
        }).ToList();
    }

    public async Task<string> GenerateInvoiceNumberAsync()
    {
        int maxNumber = 0;

        var allInvoiceNumbers = await _context.Sales
            .AsNoTracking()
            .Select(s => s.InvoiceNumber)
            .ToListAsync();

        foreach (var inv in allInvoiceNumbers)
        {
            if (!string.IsNullOrWhiteSpace(inv) && inv.StartsWith("INV-"))
            {
                var numPart = inv.Replace("INV-", "");
                if (int.TryParse(numPart, out var parsedNum) && parsedNum > maxNumber)
                {
                    maxNumber = parsedNum;
                }
            }
        }

        var nextNumber = maxNumber + 1;
        var candidate = $"INV-{nextNumber:D5}";

        while (await _context.Sales.AnyAsync(s => s.InvoiceNumber == candidate))
        {
            nextNumber++;
            candidate = $"INV-{nextNumber:D5}";
        }

        return candidate;
    }

    public async Task<Guid> CreateSaleAsync(CreateSaleDto dto, string? currentUserName = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.InvoiceNumber))
            throw new ArgumentException("شماره فاکتور فروش الزامی است.");

        var customerName = string.IsNullOrWhiteSpace(dto.CustomerName) ? "مشتری نقدی" : dto.CustomerName.Trim();

        var validItems = dto.Items
            .Where(i => i.ProductId.HasValue && i.ProductId.Value != Guid.Empty)
            .ToList();

        if (validItems.Count == 0)
            throw new ArgumentException("حداقل یک قلم کالا برای فاکتور فروش الزامی است.");

        if (validItems.Any(i => !i.WarehouseId.HasValue || i.WarehouseId.Value == Guid.Empty))
            throw new ArgumentException("برای هر ردیف کالا، انتخاب انبار الزامی است.");

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var invoiceTrimmed = dto.InvoiceNumber.Trim();

                var isDuplicateInvoice = await _context.Sales
                    .AnyAsync(s => s.InvoiceNumber == invoiceTrimmed);

                if (isDuplicateInvoice)
                    throw new InvalidOperationException($"فاکتور فروش با شماره '{invoiceTrimmed}' قبلاً ثبت شده است.");

                // دریافت قیمت خرید فعلی محصولات برای ثبت اسنپ‌شات (Snapshot)
                var distinctProductIds = validItems.Select(i => i.ProductId!.Value).Distinct().ToList();
                var purchasePrices = await _context.Products
                    .AsNoTracking()
                    .Where(p => distinctProductIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.PurchasePrice);

                var sale = new Sale(
                    invoiceNumber: invoiceTrimmed,
                    customerName: customerName,
                    warehouseId: dto.WarehouseId, // اختیاری - می‌تواند null باشد
                    saleDate: dto.SaleDate.HasValue
                        ? DateTime.SpecifyKind(dto.SaleDate.Value, DateTimeKind.Utc)
                        : DateTime.UtcNow,
                    notes: dto.Notes);

                foreach (var item in validItems)
                {
                    if (item.Quantity <= 0)
                        throw new ArgumentException("تعداد کالا باید بزرگتر از صفر باشد.");
                    if (item.UnitPrice.HasValue && item.UnitPrice.Value < 0)
                        throw new ArgumentException("قیمت واحد نمی‌تواند منفی باشد.");

                    var costPrice = purchasePrices.TryGetValue(item.ProductId!.Value, out var pp) ? pp : 0m;

                    sale.AddOrUpdateItem(
                        item.ProductId!.Value,
                        item.WarehouseId!.Value,
                        item.Quantity,
                        item.UnitPrice ?? 0m,
                        costPrice);
                }

                await _context.Sales.AddAsync(sale);

                if (dto.FinalizeImmediately)
                {
                    sale.Complete();

                    var involvedWarehouseIds = sale.Items.Select(i => i.WarehouseId).Distinct().ToList();

                    var inventories = await _context.Inventories
                        .Include(i => i.Movements)
                        .Where(i => involvedWarehouseIds.Contains(i.WarehouseId) &&
                                    distinctProductIds.Contains(i.ProductId))
                        .ToDictionaryAsync(i => (i.ProductId, i.WarehouseId));

                    foreach (var item in sale.Items)
                    {
                        if (!inventories.TryGetValue((item.ProductId, item.WarehouseId), out var inventory))
                            throw new InvalidOperationException(
                                "برای یکی از کالاها رکورد موجودی در انبار انتخاب‌شده یافت نشد.");

                        if (inventory.Quantity < item.Quantity)
                        {
                            var prod = await _context.Products.FindAsync(item.ProductId);
                            var prodName = prod != null ? prod.Name : "کالا";
                            throw new InvalidOperationException(
                                $"موجودی کالای «{prodName}» در این انبار کافی نیست. موجودی فعلی: {inventory.Quantity}، تعداد درخواستی: {item.Quantity}");
                        }

                        inventory.DecreaseQuantity(
                            amount: item.Quantity,
                            type: StockMovementType.Sale,
                            reason: $"فروش طی فاکتور {sale.InvoiceNumber}",
                            reference: sale.InvoiceNumber,
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

                return sale.Id;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"[TRANSACTION ROLLBACK] خطا در ثبت فاکتور فروش: {ex.Message}");
                throw;
            }
        });
    }

    public async Task<SaleDetailsDto?> GetSaleDetailsAsync(Guid saleId)
    {
        return await _context.Sales
            .AsNoTracking()
            .Where(s => s.Id == saleId)
            .Select(s => new SaleDetailsDto
            {
                Id = s.Id,
                InvoiceNumber = s.InvoiceNumber,
                CustomerName = s.CustomerName,
                SaleDate = s.SaleDate,
                Status = s.Status.ToString(),
                Notes = s.Notes,
                TotalAmount = s.Items.Sum(i => i.Quantity * i.UnitPrice),
                TotalCost = s.Items.Sum(i => i.Quantity * i.UnitPurchasePrice),
                Items = s.Items.Select(i => new SaleDetailsItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product != null ? i.Product.Name : "نامشخص",
                    ProductCode = i.Product != null ? i.Product.InternalCode : null,
                    ImageUrl = i.Product != null ? i.Product.ImagePath : null,
                    WarehouseName = i.Warehouse != null ? i.Warehouse.Name : "نامشخص",
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    UnitPurchasePrice = i.UnitPurchasePrice
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }
}
