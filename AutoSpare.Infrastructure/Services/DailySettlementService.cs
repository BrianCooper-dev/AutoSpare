using AutoSpare.Application.DailySettlements;
using AutoSpare.Application.DailySettlements.DTOs;
using AutoSpare.Domain.DailySettlements;
using AutoSpare.Domain.DailySettlements.Enums;
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

    public DailySettlementService(ApplicationDbContext context, ILogger<DailySettlementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<DailySettlementDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var settlements = await _context.DailySettlements
            .AsNoTracking()
            .OrderByDescending(d => d.SettlementDate)
            .ToListAsync(cancellationToken);

        if (!settlements.Any())
            return new List<DailySettlementDto>();

        var minDate = settlements.Min(s => s.SettlementDate).Date;
        var maxDate = settlements.Max(s => s.SettlementDate).Date.AddDays(1);

        // واکشی بهینه فروش‌ها با استفاده از ایندکس SaleDate
        var sales = await _context.Set<Sale>()
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed &&
                        s.SaleDate >= minDate &&
                        s.SaleDate < maxDate)
            .Select(s => new
            {
                s.SaleDate,
                Total = s.Items.Sum(i => i.Quantity * i.UnitPrice)
            })
            .ToListAsync(cancellationToken);

        // تجمیع در حافظه برای جلوگیری از پردازش سنگین در SQL
        var salesByDate = sales
            .GroupBy(s => s.SaleDate.Date)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));

        return settlements.Select(d =>
        {
            var date = d.SettlementDate.Date;
            salesByDate.TryGetValue(date, out var systemTotal);

            return new DailySettlementDto
            {
                Id = d.Id,
                SettlementDate = d.SettlementDate,
                Pos1Amount = d.Pos1Amount,
                Pos2Amount = d.Pos2Amount,
                CashAmount = d.CashAmount,
                SystemSalesAmount = systemTotal,
                Status = (int)d.Status,
                Notes = d.Notes
            };
        }).ToList();
    }

    public async Task<DailySettlementDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var settlement = await _context.DailySettlements
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (settlement == null)
            return null;

        var systemSales = await GetSystemSalesTotalAsync(settlement.SettlementDate, cancellationToken);

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

    public async Task<DailySettlementDto?> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        var nextDate = targetDate.AddDays(1);

        var settlement = await _context.DailySettlements
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.SettlementDate >= targetDate && d.SettlementDate < nextDate, cancellationToken);

        if (settlement == null)
            return null;

        var systemSales = await GetSystemSalesTotalAsync(targetDate, cancellationToken);

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

    public async Task<decimal> GetSystemSalesTotalAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        var nextDate = targetDate.AddDays(1);

        return await _context.Set<Sale>()
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed &&
                        s.SaleDate >= targetDate &&
                        s.SaleDate < nextDate)
            .SelectMany(s => s.Items)
            .SumAsync(i => (decimal?)i.Quantity * i.UnitPrice, cancellationToken) ?? 0m;
    }

    public async Task<Guid> UpsertAsync(UpsertDailySettlementDto dto, CancellationToken cancellationToken = default)
    {
        if (!dto.SettlementDate.HasValue || dto.SettlementDate.Value == default)
            throw new InvalidOperationException("تاریخ تسویه الزامی است.");

        var targetDate = dto.SettlementDate.Value.Date;
        var nextDate = targetDate.AddDays(1);

        try
        {
            if (dto.Id.HasValue && dto.Id.Value != Guid.Empty)
            {
                var existing =
                    await _context.DailySettlements.FindAsync(new object[] { dto.Id.Value }, cancellationToken);
                if (existing == null)
                    throw new InvalidOperationException("تسویه مورد نظر یافت نشد.");

                var duplicateDate = await _context.DailySettlements
                    .AnyAsync(
                        d => d.SettlementDate >= targetDate && d.SettlementDate < nextDate && d.Id != dto.Id.Value,
                        cancellationToken);

                if (duplicateDate)
                    throw new InvalidOperationException("برای این تاریخ قبلاً تسویه ثبت شده است.");

                if (existing.SettlementDate.Date != targetDate)
                    throw new InvalidOperationException("تغییر تاریخ تسویه پس از ثبت امکان‌پذیر نیست.");

                existing.UpdateAmounts(dto.Pos1Amount, dto.Pos2Amount, dto.CashAmount, dto.Notes);
                await _context.SaveChangesAsync(cancellationToken);
                return existing.Id;
            }

            var isDuplicate = await _context.DailySettlements
                .AnyAsync(d => d.SettlementDate >= targetDate && d.SettlementDate < nextDate, cancellationToken);

            if (isDuplicate)
                throw new InvalidOperationException("برای این تاریخ قبلاً تسویه ثبت شده است.");

            var newSettlement = new DailySettlement(
                targetDate,
                dto.Pos1Amount,
                dto.Pos2Amount,
                dto.CashAmount,
                dto.Notes
            );

            await _context.DailySettlements.AddAsync(newSettlement, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return newSettlement.Id;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "خطای پایگاه داده در تسویه روزانه برای تاریخ {SettlementDate}", targetDate);
            throw new InvalidOperationException(
                "خطا در ذخیره‌سازی اطلاعات تسویه. ممکن است تسویه همزمانی برای این تاریخ ثبت شده باشد.", ex);
        }
    }

    public async Task CloseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.DailySettlements.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
            throw new InvalidOperationException("تسویه مورد نظر یافت نشد.");

        entity.CloseSettlement();
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReopenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.DailySettlements.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
            throw new InvalidOperationException("تسویه مورد نظر یافت نشد.");

        entity.ReopenSettlement();
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.DailySettlements.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
            throw new InvalidOperationException("تسویه مورد نظر یافت نشد.");

        _context.DailySettlements.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
