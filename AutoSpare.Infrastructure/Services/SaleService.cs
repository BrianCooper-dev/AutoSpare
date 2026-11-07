using System.Globalization;
using AutoSpare.Application.Products.DTOs;
using AutoSpare.Application.Sales;
using AutoSpare.Application.Sales.DTOs;
using AutoSpare.Domain.Inventories;
using AutoSpare.Domain.Inventories.Enums;
using AutoSpare.Domain.Sales;
using AutoSpare.Domain.Sales.Enums;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoSpare.Infrastructure.Services;

public class SaleService : ISaleService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SaleService> _logger;

    public SaleService(
        ApplicationDbContext context,
        ILogger<SaleService> logger)
    {
        _context = context;
        _logger = logger;
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
        var products = await _context.Products
            .AsNoTracking()
            .Where(p =>
                p.Status == Domain.Products.Enums.ProductStatus.Active)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.InternalCode,
                p.Model,
                BrandName = p.Brand != null ? p.Brand.Name : null,
                p.PurchasePrice,
                p.SalePrice,
                p.ImagePath
            })
            .OrderBy(p => p.Name)
            .ToListAsync();

        var productIds = products
            .Select(p => p.Id)
            .ToList();

        var stocks = await _context.Inventories
            .AsNoTracking()
            .Where(i =>
                productIds.Contains(i.ProductId) &&
                i.Quantity > 0)
            .Select(i => new
            {
                i.ProductId,
                i.WarehouseId,
                i.Quantity
            })
            .ToListAsync();

        var stocksLookup = stocks
            .GroupBy(i => i.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(
                    x => x.WarehouseId,
                    x => x.Quantity));

        return products.Select(p =>
        {
            var perWarehouse =
                stocksLookup.TryGetValue(p.Id, out var stock)
                    ? stock
                    : new Dictionary<Guid, int>();

            var defaultWarehouse = perWarehouse
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Value)
                .Select(x => (Guid?)x.Key)
                .FirstOrDefault();

            return new SaleProductLookupDto(
                p.Id,
                p.Name,
                p.InternalCode,
                p.Model,
                p.BrandName,
                p.PurchasePrice,
                p.SalePrice,
                p.ImagePath,
                perWarehouse,
                defaultWarehouse,
                perWarehouse.Values.Sum());
        }).ToList();
    }

    public async Task<string> GenerateInvoiceNumberAsync()
    {
        var persianCalendar = new PersianCalendar();
        var currentPersianYear =
            persianCalendar.GetYear(DateTime.UtcNow);

        var prefix = $"INV-{currentPersianYear}-";

        var latestInvoice = await _context.Sales
            .AsNoTracking()
            .Where(s => s.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(s => s.InvoiceNumber)
            .Select(s => s.InvoiceNumber)
            .FirstOrDefaultAsync();

        var nextNumber = 1;

        if (!string.IsNullOrWhiteSpace(latestInvoice))
        {
            var raw = latestInvoice.Replace(prefix, "");

            if (int.TryParse(raw, out var parsed))
            {
                nextNumber = parsed + 1;
            }
        }

        var candidate = $"{prefix}{nextNumber:D4}";

        while (await _context.Sales
                   .AnyAsync(s => s.InvoiceNumber == candidate))
        {
            nextNumber++;
            candidate = $"{prefix}{nextNumber:D4}";
        }

        return candidate;
    }

    public async Task<Guid> CreateSaleAsync(
        CreateSaleDto dto,
        string? currentUserName = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.InvoiceNumber))
        {
            throw new ArgumentException(
                "شماره فاکتور فروش الزامی است.");
        }

        var customerName =
            string.IsNullOrWhiteSpace(dto.CustomerName)
                ? "مشتری نقدی"
                : dto.CustomerName.Trim();

        var validItems = dto.Items
            .Where(i =>
                i.ProductId.HasValue &&
                i.ProductId.Value != Guid.Empty)
            .ToList();

        if (validItems.Count == 0)
        {
            throw new ArgumentException(
                "حداقل یک قلم کالا برای فاکتور فروش الزامی است.");
        }

        if (validItems.Any(i =>
                !i.WarehouseId.HasValue ||
                i.WarehouseId.Value == Guid.Empty))
        {
            throw new ArgumentException(
                "برای هر ردیف کالا، انتخاب انبار الزامی است.");
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            var retryCount = 3;

            // طبق رفتار کد قبلی، شماره فاکتور برای این متد الزامی است.
            var isAutoGenerated =
                string.IsNullOrWhiteSpace(dto.InvoiceNumber);

            while (retryCount > 0)
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync();

                try
                {
                    var invoiceTrimmed = isAutoGenerated
                        ? await GenerateInvoiceNumberAsync()
                        : dto.InvoiceNumber.Trim();

                    var isDuplicateInvoice =
                        await _context.Sales.AnyAsync(s => s.InvoiceNumber == invoiceTrimmed);

                    if (isDuplicateInvoice)
                    {
                        if (!isAutoGenerated)
                        {
                            throw new InvalidOperationException(
                                $"فاکتور فروش با شماره '{invoiceTrimmed}' قبلاً ثبت شده است.");
                        }

                        retryCount--;
                        await transaction.RollbackAsync();
                        continue;
                    }

                    var distinctProductIds = validItems
                        .Select(i => i.ProductId!.Value)
                        .Distinct()
                        .ToList();

                    var productInfos = await _context.Products
                        .AsNoTracking()
                        .Where(p =>
                            distinctProductIds.Contains(p.Id))
                        .ToDictionaryAsync(p => p.Id);

                    var missingIds = distinctProductIds
                        .Except(productInfos.Keys)
                        .ToList();

                    if (missingIds.Count > 0)
                    {
                        throw new InvalidOperationException(
                            "یک یا چند کالای انتخاب‌شده در سیستم وجود ندارند.");
                    }

                    var sale = new Sale(
                        invoiceNumber: invoiceTrimmed,
                        customerName: customerName,
                        warehouseId: dto.WarehouseId,
                        saleDate: dto.SaleDate.HasValue
                            ? DateTime.SpecifyKind(
                                dto.SaleDate.Value,
                                DateTimeKind.Utc)
                            : DateTime.UtcNow,
                        notes: dto.Notes);

                    foreach (var item in validItems)
                    {
                        if (item.Quantity <= 0)
                        {
                            throw new ArgumentException(
                                "تعداد کالا باید بزرگ‌تر از صفر باشد.");
                        }

                        if (item.UnitPrice.HasValue &&
                            item.UnitPrice.Value < 0)
                        {
                            throw new ArgumentException(
                                "قیمت واحد نمی‌تواند منفی باشد.");
                        }

                        var product =
                            productInfos[item.ProductId!.Value];

                        sale.AddOrUpdateItem(
                            item.ProductId!.Value,
                            item.WarehouseId!.Value,
                            item.Quantity,
                            item.UnitPrice ?? 0m,
                            product.PurchasePrice);
                    }

                    var involvedWarehouseIds = sale.Items
                        .Select(i => i.WarehouseId)
                        .Distinct()
                        .ToList();

                    var inventories = await _context.Inventories
                        .Include(i => i.Movements)
                        .Where(i =>
                            involvedWarehouseIds.Contains(
                                i.WarehouseId) &&
                            distinctProductIds.Contains(
                                i.ProductId))
                        .ToDictionaryAsync(i => (i.ProductId, i.WarehouseId));

                    // این بررسی برای پیش‌نویس و فاکتور نهایی انجام می‌شود.
                    // جمع‌زدن اقلام هم‌کالا در یک انبار مانع دور زدن
                    // محدودیت موجودی با چند ردیف جداگانه می‌شود.
                    var requestedStocks = sale.Items
                        .GroupBy(i => new
                        {
                            i.ProductId,
                            i.WarehouseId
                        })
                        .Select(g => new
                        {
                            g.Key.ProductId,
                            g.Key.WarehouseId,
                            Quantity = g.Sum(i => i.Quantity)
                        })
                        .ToList();

                    foreach (var requested in requestedStocks)
                    {
                        var productName =
                            productInfos[requested.ProductId].Name;

                        if (!inventories.TryGetValue(
                                (
                                    requested.ProductId,
                                    requested.WarehouseId
                                ),
                                out var inventory))
                        {
                            throw new InvalidOperationException(
                                $"رکورد موجودی برای کالای «{productName}» در انبار انتخاب‌شده یافت نشد.");
                        }

                        if (inventory.Quantity <= 0)
                        {
                            throw new InvalidOperationException(
                                $"موجودی کالای «{productName}» در انبار انتخاب‌شده صفر است و امکان ثبت فاکتور وجود ندارد.");
                        }

                        if (inventory.Quantity <
                            requested.Quantity)
                        {
                            throw new InvalidOperationException(
                                $"موجودی کالای «{productName}» کافی نیست. موجودی فعلی: {inventory.Quantity}");
                        }
                    }

                    // فقط بعد از موفقیت تمام بررسی‌ها، فاکتور ثبت شود.
                    await _context.Sales.AddAsync(sale);

                    if (dto.FinalizeImmediately)
                    {
                        sale.Complete();

                        foreach (var item in sale.Items)
                        {
                            var inventory = inventories[
                                (item.ProductId, item.WarehouseId)];

                            inventory.DecreaseQuantity(
                                amount: item.Quantity,
                                type: StockMovementType.Sale,
                                reason:
                                $"فروش طی فاکتور {sale.InvoiceNumber}",
                                reference: sale.InvoiceNumber,
                                performedBy:
                                currentUserName ?? "سیستم");

                            MarkLatestMovementAsAdded(inventory);
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return sale.Id;
                }
                catch (DbUpdateException ex)
                    when (isAutoGenerated &&
                          (ex.InnerException?.Message.Contains(
                               "UNIQUE") == true ||
                           ex.InnerException?.Message.Contains(
                               "IX_") == true))
                {
                    await transaction.RollbackAsync();
                    retryCount--;

                    if (retryCount == 0)
                    {
                        throw new InvalidOperationException(
                            "به دلیل همزمانی، امکان تولید شماره فاکتور یکتا میسر نشد.",
                            ex);
                    }
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            throw new InvalidOperationException(
                "خطای ناشناخته در ثبت فاکتور فروش.");
        });
    }

    public async Task<SaleDetailsDto?> GetSaleDetailsAsync(
        Guid saleId)
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
                CancelledAt = s.CancelledAt,
                CancellationReason = s.CancellationReason,
                TotalAmount =
                    s.Items.Sum(i =>
                        i.Quantity * i.UnitPrice),
                TotalCost =
                    s.Items.Sum(i =>
                        i.Quantity * i.UnitPurchasePrice),
                Items = s.Items
                    .Select(i => new SaleDetailsItemDto
                    {
                        Id = i.Id,
                        ProductId = i.ProductId,
                        ProductName = i.Product != null
                            ? i.Product.Name
                            : "نامشخص",
                        ProductCode = i.Product != null
                            ? i.Product.InternalCode
                            : null,
                        ImageUrl = i.Product != null
                            ? i.Product.ImagePath
                            : null,
                        WarehouseName = i.Warehouse != null
                            ? i.Warehouse.Name
                            : "نامشخص",
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        UnitPurchasePrice =
                            i.UnitPurchasePrice
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<SaleListDto>> GetSalesAsync(
        string? search = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var query = _context.Sales
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(s =>
                s.InvoiceNumber.Contains(term) ||
                s.CustomerName.Contains(term));
        }

        if (fromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(
                fromDate.Value.Date,
                DateTimeKind.Utc);

            query = query.Where(s =>
                s.SaleDate >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(
                toDate.Value.Date
                    .AddDays(1)
                    .AddTicks(-1),
                DateTimeKind.Utc);

            query = query.Where(s =>
                s.SaleDate <= toUtc);
        }

        return await query
            .OrderByDescending(s => s.SaleDate)
            .Select(s => new SaleListDto
            {
                Id = s.Id,
                InvoiceNumber = s.InvoiceNumber,
                CustomerName = s.CustomerName,
                WarehouseName = s.Warehouse != null
                    ? s.Warehouse.Name
                    : "چند انبار / نامشخص",
                SaleDate = s.SaleDate,
                Status = s.Status,
                ItemsCount = s.Items.Count,
                TotalAmount = s.Items.Sum(i =>
                    i.Quantity * i.UnitPrice)
            })
            .ToListAsync();
    }

    public async Task<bool> FinalizeSaleAsync(
        Guid saleId,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    cancellationToken);

            try
            {
                var sale = await _context.Sales
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(
                        s => s.Id == saleId,
                        cancellationToken);

                if (sale is null)
                {
                    throw new InvalidOperationException(
                        "فاکتور مورد نظر یافت نشد.");
                }

                if (sale.Status != SaleStatus.Draft)
                {
                    throw new InvalidOperationException(
                        "تنها فاکتورهای پیش‌نویس قابل نهایی‌سازی هستند.");
                }

                if (!sale.Items.Any())
                {
                    throw new InvalidOperationException(
                        "فاکتور بدون کالا قابل نهایی‌سازی نیست.");
                }

                if (sale.Items.Any(i => i.Quantity <= 0))
                {
                    throw new InvalidOperationException(
                        "تعداد کالا باید بزرگ‌تر از صفر باشد.");
                }

                var involvedWarehouseIds = sale.Items
                    .Select(i => i.WarehouseId)
                    .Distinct()
                    .ToList();

                var distinctProductIds = sale.Items
                    .Select(i => i.ProductId)
                    .Distinct()
                    .ToList();

                var productNames = await _context.Products
                    .AsNoTracking()
                    .Where(p =>
                        distinctProductIds.Contains(p.Id))
                    .ToDictionaryAsync(
                        p => p.Id,
                        p => p.Name,
                        cancellationToken);

                var inventories = await _context.Inventories
                    .Include(i => i.Movements)
                    .Where(i =>
                        involvedWarehouseIds.Contains(
                            i.WarehouseId) &&
                        distinctProductIds.Contains(
                            i.ProductId))
                    .ToDictionaryAsync(
                        i => (i.ProductId, i.WarehouseId),
                        cancellationToken);

                // موجودی از زمان ثبت پیش‌نویس ممکن است تغییر کرده باشد.
                var requestedStocks = sale.Items
                    .GroupBy(i => new
                    {
                        i.ProductId,
                        i.WarehouseId
                    })
                    .Select(g => new
                    {
                        g.Key.ProductId,
                        g.Key.WarehouseId,
                        Quantity = g.Sum(i => i.Quantity)
                    })
                    .ToList();

                foreach (var requested in requestedStocks)
                {
                    var productName =
                        productNames.TryGetValue(
                            requested.ProductId,
                            out var name)
                            ? name
                            : "کالا";

                    if (!inventories.TryGetValue(
                            (
                                requested.ProductId,
                                requested.WarehouseId
                            ),
                            out var inventory))
                    {
                        throw new InvalidOperationException(
                            $"رکورد موجودی برای کالای «{productName}» در انبار انتخاب‌شده یافت نشد.");
                    }

                    if (inventory.Quantity <= 0)
                    {
                        throw new InvalidOperationException(
                            $"موجودی کالای «{productName}» صفر است و امکان نهایی‌سازی فاکتور وجود ندارد.");
                    }

                    if (inventory.Quantity <
                        requested.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"موجودی کالای «{productName}» در این انبار کافی نیست. موجودی فعلی: {inventory.Quantity}");
                    }
                }

                // پس از تأیید تمام اقلام، وضعیت و موجودی تغییر می‌کند.
                sale.Complete();

                foreach (var item in sale.Items)
                {
                    var inventory = inventories[
                        (item.ProductId, item.WarehouseId)];

                    inventory.DecreaseQuantity(
                        amount: item.Quantity,
                        type: StockMovementType.Sale,
                        reason:
                        $"فروش طی فاکتور {sale.InvoiceNumber}",
                        reference: sale.InvoiceNumber,
                        performedBy: "سیستم");

                    MarkLatestMovementAsAdded(inventory);
                }

                await _context.SaveChangesAsync(
                    cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);

                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                LogConcurrencyDetails(ex);

                await transaction.RollbackAsync(
                    cancellationToken);

                throw new InvalidOperationException(
                    "رکورد موجودی همزمان تغییر کرده است. لطفاً دوباره تلاش کنید.",
                    ex);
            }
            catch
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                throw;
            }
        });
    }

    public async Task CancelSaleAsync(
        Guid saleId,
        string? reason = null)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var sale = await _context.Sales
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(s =>
                        s.Id == saleId);

                if (sale is null)
                {
                    throw new InvalidOperationException(
                        "فاکتور یافت نشد.");
                }

                if (sale.Status == SaleStatus.Cancelled)
                {
                    throw new InvalidOperationException(
                        "این فاکتور قبلاً باطل شده است.");
                }

                var effectiveReason =
                    string.IsNullOrWhiteSpace(reason)
                        ? "بدون درج دلیل"
                        : reason.Trim();

                if (sale.Status == SaleStatus.Completed)
                {
                    var productIds = sale.Items
                        .Select(i => i.ProductId)
                        .Distinct()
                        .ToList();

                    var warehouseIds = sale.Items
                        .Select(i => i.WarehouseId)
                        .Distinct()
                        .ToList();

                    var inventories =
                        await _context.Inventories
                            .Include(i => i.Movements)
                            .Where(i =>
                                productIds.Contains(
                                    i.ProductId) &&
                                warehouseIds.Contains(
                                    i.WarehouseId))
                            .ToDictionaryAsync(i => (
                                i.ProductId,
                                i.WarehouseId
                            ));

                    foreach (var item in sale.Items)
                    {
                        if (!inventories.TryGetValue(
                                (
                                    item.ProductId,
                                    item.WarehouseId
                                ),
                                out var inventory))
                        {
                            throw new InvalidOperationException(
                                "رکورد انبار برای کالا یافت نشد.");
                        }

                        inventory.IncreaseQuantity(
                            amount: item.Quantity,
                            type:
                            StockMovementType.CustomerReturn,
                            reason:
                            $"ابطال فاکتور فروش {sale.InvoiceNumber} - دلیل: {effectiveReason}",
                            reference: sale.InvoiceNumber,
                            performedBy: "سیستم");

                        MarkLatestMovementAsAdded(inventory);
                    }
                }

                sale.Cancel(effectiveReason);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                LogConcurrencyDetails(ex);

                await transaction.RollbackAsync();

                throw new InvalidOperationException(
                    "رکورد موجودی همزمان تغییر کرده است. لطفاً دوباره تلاش کنید.",
                    ex);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    private void MarkLatestMovementAsAdded(
        Inventory inventory)
    {
        var movement =
            inventory.Movements.LastOrDefault();

        if (movement == null)
        {
            throw new InvalidOperationException(
                "پس از تغییر موجودی، حرکت انبار جدیدی ایجاد نشد.");
        }

        _context.Entry(movement).State =
            EntityState.Added;
    }

    private void LogConcurrencyDetails(
        DbUpdateConcurrencyException exception)
    {
        _logger.LogError(
            exception,
            "[SaleService] Concurrency conflict occurred.");
    }
}
