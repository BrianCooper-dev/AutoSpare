using AutoSpare.Application.DailySettlements;
using AutoSpare.Application.DailySettlements.DTOs;
using AutoSpare.Domain.DailySettlements;
using AutoSpare.Domain.Sales;
using AutoSpare.Domain.Sales.Enums;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoSpare.Infrastructure.Services;

public class DailySettlementService : IDailySettlementService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DailySettlementService> _logger;

    public DailySettlementService(
        ApplicationDbContext context,
        ILogger<DailySettlementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<DailySettlementDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var settlements = await _context.DailySettlements
            .AsNoTracking()
            .OrderByDescending(d => d.SettlementDate)
            .ToListAsync(cancellationToken);

        if (settlements.Count == 0)
            return new List<DailySettlementDto>();

        var minDate = settlements.Min(d => d.SettlementDate).Date;
        var maxDate = settlements.Max(d => d.SettlementDate).Date.AddDays(1);

        // فقط داده‌های لازم را از دیتابیس می‌گیریم؛ جمع decimal در حافظه محاسبه می‌شود.
        var saleItems = await _context.Set<Sale>()
            .AsNoTracking()
            .Where(s =>
                s.Status == SaleStatus.Completed &&
                s.SaleDate >= minDate &&
                s.SaleDate < maxDate)
            .SelectMany(
                s => s.Items,
                (sale, item) => new
                {
                    sale.SaleDate,
                    item.Quantity,
                    item.UnitPrice
                })
            .ToListAsync(cancellationToken);

        var salesDict = saleItems
            .GroupBy(x => x.SaleDate.Date)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.Quantity * x.UnitPrice));

        return settlements.Select(d =>
        {
            salesDict.TryGetValue(d.SettlementDate.Date, out var systemSales);

            return new DailySettlementDto
            {
                Id = d.Id,
                SettlementDate = d.SettlementDate,
                Pos1Amount = d.Pos1Amount,
                Pos2Amount = d.Pos2Amount,
                CashAmount = d.CashAmount,
                SystemSalesAmount = systemSales,
                Status = (int)d.Status,
                Notes = d.Notes
            };
        }).ToList();
    }

    public async Task<DailySettlementDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var settlement = await _context.DailySettlements
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (settlement == null)
            return null;

        var systemSales = await GetSystemSalesTotalAsync(
            settlement.SettlementDate,
            cancellationToken);

        return new DailySettlementDto
        {
            Id = settlement.Id,
            SettlementDate = settlement.SettlementDate,
            Pos1Amount = settlement.Pos1Amount,
            Pos2Amount = settlement.Pos2Amount,
            CashAmount = settlement.CashAmount,
            SystemSalesAmount = systemSales,
            Status = (int)settlement.Status,
            Notes = settlement.Notes
        };
    }

    public async Task<DailySettlementDto?> GetByDateAsync(
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        var nextDate = targetDate.AddDays(1);

        var settlement = await _context.DailySettlements
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.SettlementDate >= targetDate &&
                     d.SettlementDate < nextDate,
                cancellationToken);

        if (settlement == null)
            return null;

        var systemSales = await GetSystemSalesTotalAsync(
            targetDate,
            cancellationToken);

        return new DailySettlementDto
        {
            Id = settlement.Id,
            SettlementDate = settlement.SettlementDate,
            Pos1Amount = settlement.Pos1Amount,
            Pos2Amount = settlement.Pos2Amount,
            CashAmount = settlement.CashAmount,
            SystemSalesAmount = systemSales,
            Status = (int)settlement.Status,
            Notes = settlement.Notes
        };
    }

    public async Task<decimal> GetSystemSalesTotalAsync(
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        var nextDate = targetDate.AddDays(1);

        var saleItems = await _context.Set<Sale>()
            .AsNoTracking()
            .Where(s =>
                s.Status == SaleStatus.Completed &&
                s.SaleDate >= targetDate &&
                s.SaleDate < nextDate)
            .SelectMany(s => s.Items)
            .Select(i => new
            {
                i.Quantity,
                i.UnitPrice
            })
            .ToListAsync(cancellationToken);

        return saleItems.Sum(i => i.Quantity * i.UnitPrice);
    }

    public async Task<Guid> UpsertAsync(
        UpsertDailySettlementDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!dto.SettlementDate.HasValue)
            throw new ArgumentException(
                "تاریخ تسویه نامعتبر است.",
                nameof(dto));

        var targetDate = dto.SettlementDate.Value.Date;
        var nextDate = targetDate.AddDays(1);

        if (dto.Id.HasValue && dto.Id.Value != Guid.Empty)
        {
            var existing = await _context.DailySettlements.FindAsync(
                new object[] { dto.Id.Value },
                cancellationToken);

            if (existing == null)
                throw new KeyNotFoundException(
                    "رکورد تسویه مورد نظر یافت نشد.");

            if (existing.SettlementDate.Date != targetDate)
                throw new InvalidOperationException(
                    "امکان تغییر تاریخ تسویه وجود ندارد.");

            var duplicateExists = await _context.DailySettlements
                .AnyAsync(
                    d => d.SettlementDate >= targetDate &&
                         d.SettlementDate < nextDate &&
                         d.Id != dto.Id.Value,
                    cancellationToken);

            if (duplicateExists)
                throw new InvalidOperationException(
                    "برای این تاریخ قبلاً تسویه ثبت شده است.");

            try
            {
                existing.UpdateAmounts(
                    dto.Pos1Amount,
                    dto.Pos2Amount,
                    dto.CashAmount,
                    dto.Notes?.Trim());

                await _context.SaveChangesAsync(cancellationToken);
                return existing.Id;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "خطا در به‌روزرسانی تسویه روزانه {Id}",
                    existing.Id);

                throw;
            }
        }

        var exists = await _context.DailySettlements
            .AnyAsync(
                d => d.SettlementDate >= targetDate &&
                     d.SettlementDate < nextDate,
                cancellationToken);

        if (exists)
            throw new InvalidOperationException(
                "برای این تاریخ قبلاً تسویه ثبت شده است.");

        try
        {
            var settlement = new DailySettlement(
                targetDate,
                dto.Pos1Amount,
                dto.Pos2Amount,
                dto.CashAmount,
                dto.Notes?.Trim());

            await _context.DailySettlements.AddAsync(
                settlement,
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            return settlement.Id;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "خطا در ثبت تسویه روزانه جدید برای تاریخ {Date}",
                targetDate);

            throw;
        }
    }

    public async Task CloseAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.DailySettlements.FindAsync(
            new object[] { id },
            cancellationToken);

        if (entity == null)
            throw new KeyNotFoundException(
                "رکورد تسویه یافت نشد.");

        try
        {
            entity.CloseSettlement();
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "خطا در بستن تسویه {Id}",
                id);

            throw;
        }
    }

    public async Task ReopenAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.DailySettlements.FindAsync(
            new object[] { id },
            cancellationToken);

        if (entity == null)
            throw new KeyNotFoundException(
                "رکورد تسویه یافت نشد.");

        try
        {
            entity.ReopenSettlement();
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "خطا در باز کردن مجدد تسویه {Id}",
                id);

            throw;
        }
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.DailySettlements.FindAsync(
            new object[] { id },
            cancellationToken);

        if (entity == null)
            throw new KeyNotFoundException(
                "رکورد تسویه یافت نشد.");

        try
        {
            _context.DailySettlements.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "خطا در حذف تسویه {Id}",
                id);

            throw;
        }
    }
}
