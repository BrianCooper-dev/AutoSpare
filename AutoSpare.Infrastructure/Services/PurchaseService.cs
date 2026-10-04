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
            .Select(p => new ProductLookupItemDto(p.Id, p.Name, p.InternalCode, p.PurchasePrice))
            .ToListAsync();

    public async Task<Guid> CreatePurchaseAsync(CreatePurchaseDto dto, string? currentUserName = null)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.InvoiceNumber))
            throw new ArgumentException("شماره فاکتور خرید الزامی است.", nameof(dto));

        if (!dto.SupplierId.HasValue || dto.SupplierId.Value == Guid.Empty)
            throw new ArgumentException("انتخاب تأمین‌کننده الزامی است.", nameof(dto));

        if (!dto.WarehouseId.HasValue || dto.WarehouseId.Value == Guid.Empty)
            throw new ArgumentException("انتخاب انبار الزامی است.", nameof(dto));

        if (dto.Items == null || !dto.Items.Any())
            throw new ArgumentException("ثبت حداقل یک قلم کالا برای فاکتور خرید الزامی است.", nameof(dto));

        var validItems = dto.Items.Where(i => i.ProductId.HasValue && i.ProductId.Value != Guid.Empty).ToList();
        if (!validItems.Any())
            throw new ArgumentException("هیچ کالای معتبری در ردیف‌های خرید انتخاب نشده است.", nameof(dto));

        // بررسی یکتایی شماره فاکتور
        var isDuplicateInvoice = await _context.Purchases
            .AnyAsync(p => p.InvoiceNumber == dto.InvoiceNumber.Trim());
        if (isDuplicateInvoice)
            throw new InvalidOperationException($"فاکتور خرید با شماره '{dto.InvoiceNumber}' قبلاً ثبت شده است.");

        // ۱. ساخت موجودیت خرید
        var purchase = new Purchase(
            invoiceNumber: dto.InvoiceNumber.Trim(),
            supplierId: dto.SupplierId.Value,
            warehouseId: dto.WarehouseId.Value,
            purchaseDate: dto.PurchaseDate.HasValue
                ? DateTime.SpecifyKind(dto.PurchaseDate.Value, DateTimeKind.Utc)
                : DateTime.UtcNow,
            notes: dto.Notes
        );

        foreach (var item in validItems)
        {
            if (item.Quantity <= 0)
                throw new ArgumentException("تعداد کالای خریداری شده باید بزرگتر از صفر باشد.");
            if (item.UnitPrice < 0)
                throw new ArgumentException("قیمت واحد نمی‌تواند منفی باشد.");

            purchase.AddOrUpdateItem(item.ProductId!.Value, item.Quantity, item.UnitPrice);
        }

        await _context.Purchases.AddAsync(purchase);

        // ۲. در صورت نهایی‌سازی و افزایش فوری موجودی انبار:
        // ۲. در صورت نهایی‌سازی و افزایش فوری موجودی انبار:
        if (dto.FinalizeImmediately)
        {
            purchase.Complete();

            foreach (var item in validItems)
            {
                var productId = item.ProductId!.Value;
                var warehouseId = dto.WarehouseId.Value;

                // دریافت کالا برای به‌روزرسانی قیمت
                var product = await _context.Products.FindAsync(productId);
                if (product != null && item.UnitPrice > 0)
                {
                    var newSalePrice = Math.Max(product.SalePrice, item.UnitPrice);
                    product.UpdatePrices(item.UnitPrice, newSalePrice);
                }

                // بررسی وجود رکورد موجودی بدون Include اضافی
                var inventory = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId);

                if (inventory == null)
                {
                    // رکورد جدید
                    var newInventory = new Inventory(productId, warehouseId, initialQuantity: item.Quantity);
                    await _context.Inventories.AddAsync(newInventory);
                }
                else
                {
                    // افزایش موجودی
                    inventory.IncreaseQuantity(
                        amount: item.Quantity,
                        type: StockMovementType.Purchase,
                        reason: $"خرید طی فاکتور {purchase.InvoiceNumber}",
                        reference: purchase.InvoiceNumber,
                        performedBy: currentUserName ?? "سیستم"
                    );

                    // اطمینان از این‌که حرکات اضافه شده جدید در وضعیت Added قرار می‌گیرند
                    foreach (var movement in inventory.Movements)
                    {
                        var entry = _context.Entry(movement);
                        if (entry.State == EntityState.Detached || entry.State == EntityState.Modified)
                        {
                            entry.State = EntityState.Added;
                        }
                    }
                }
            }
        }


        // ذخیره یکپارچه کلیه تغییرات
        // لاگ وضعیت همه‌ی انتیتی‌ها قبل از ذخیره
        foreach (var entry in _context.ChangeTracker.Entries())
        {
            Console.WriteLine($"[EF STATE] {entry.Entity.GetType().Name} => {entry.State}");
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                Console.WriteLine(
                    $"[CONCURRENCY CULPRIT] Entity: {entry.Entity.GetType().FullName}, State: {entry.State}");
            }

            throw;
        }
        catch (DbUpdateException ex)
        {
            Console.WriteLine($"[DB UPDATE ERROR] {ex.InnerException?.Message}");
            throw;
        }


        return purchase.Id;
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
        // دریافت آخرین شماره فاکتورهای موجود
        var lastInvoice = await _context.Purchases
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt) // یا بر اساس Id
            .Select(p => p.InvoiceNumber)
            .FirstOrDefaultAsync();

        int maxNumber = 0;

        // اگر فاکتوری از قبل بود، سعی می‌کنیم عدد آن را استخراج کنیم
        var allInvoiceNumbers = await _context.Purchases
            .AsNoTracking()
            .Select(p => p.InvoiceNumber)
            .ToListAsync();

        foreach (var inv in allInvoiceNumbers)
        {
            if (!string.IsNullOrWhiteSpace(inv) && inv.StartsWith("PUR-"))
            {
                var numPart = inv.Replace("PUR-", "");
                if (int.TryParse(numPart, out int parsedNum))
                {
                    if (parsedNum > maxNumber)
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
