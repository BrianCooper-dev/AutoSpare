using AutoSpare.Application.DailySettlements;
using AutoSpare.Application.DailySettlements.DTOs;
using AutoSpare.Domain.DailySettlements;
using AutoSpare.Domain.DailySettlements.Enums;
using AutoSpare.Domain.Sales;
using AutoSpare.Domain.Sales.Enums;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Infrastructure.Services;

public class DailySettlementService : IDailySettlementService
{
    private readonly ApplicationDbContext _context;

    public DailySettlementService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DailySettlementDto>> GetAllAsync()
    {
        var settlements = await _context.DailySettlements
            .OrderByDescending(d => d.SettlementDate)
            .ToListAsync();

        if (!settlements.Any())
            return new List<DailySettlementDto>();

        // بازه تاریخ برای واکشی یکباره فروش‌های سیستم
        var minDate = settlements.Min(s => s.SettlementDate).Date;
        var maxDate = settlements.Max(s => s.SettlementDate).Date.AddDays(1);

        // واکشی فاکتورهای تکمیل‌شده با اقلام آنها
        var sales = await _context.Set<Sale>()
            .Where(s => s.Status == SaleStatus.Completed &&
                        s.SaleDate >= minDate &&
                        s.SaleDate < maxDate)
            .Select(s => new
            {
                Date = s.SaleDate.Date,
                Total = s.Items.Sum(i => i.Quantity * i.UnitPrice)
            })
            .ToListAsync();

        // تجمیع فروش بر اساس تاریخ روز
        var salesByDate = sales
            .GroupBy(s => s.Date)
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

    public async Task<DailySettlementDto?> GetByIdAsync(Guid id)
    {
        var settlement = await _context.DailySettlements.FindAsync(id);
        if (settlement == null)
            return null;

        var systemSales = await GetSystemSalesTotalAsync(settlement.SettlementDate);

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

    public async Task<DailySettlementDto?> GetByDateAsync(DateTime date)
    {
        var targetDate = date.Date;
        var settlement = await _context.DailySettlements
            .FirstOrDefaultAsync(d => d.SettlementDate == targetDate);

        if (settlement == null)
            return null;

        var systemSales = await GetSystemSalesTotalAsync(targetDate);

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

    public async Task<decimal> GetSystemSalesTotalAsync(DateTime date)
    {
        var targetDate = date.Date;
        var nextDate = targetDate.AddDays(1);

        return await _context.Set<Sale>()
            .Where(s => s.Status == SaleStatus.Completed &&
                        s.SaleDate >= targetDate &&
                        s.SaleDate < nextDate)
            .SelectMany(s => s.Items)
            .SumAsync(i => (decimal?)i.Quantity * i.UnitPrice) ?? 0m;
    }

    public async Task<Guid> UpsertAsync(UpsertDailySettlementDto dto)
    {
        if (!dto.SettlementDate.HasValue || dto.SettlementDate.Value == default)
            throw new InvalidOperationException("تاریخ تسویه الزامی است.");

        var targetDate = dto.SettlementDate.Value.Date;

        if (dto.Id.HasValue && dto.Id.Value != Guid.Empty)
        {
            var existing = await _context.DailySettlements.FindAsync(dto.Id.Value);
            if (existing == null)
                throw new InvalidOperationException("تسویه مورد نظر یافت نشد.");

            var duplicateDate = await _context.DailySettlements
                .AnyAsync(d => d.SettlementDate == targetDate && d.Id != dto.Id.Value);

            if (duplicateDate)
                throw new InvalidOperationException("برای این تاریخ قبلاً تسویه ثبت شده است.");

            if (existing.SettlementDate.Date != targetDate)
                throw new InvalidOperationException("تغییر تاریخ تسویه پس از ثبت امکان‌پذیر نیست.");

            existing.UpdateAmounts(dto.Pos1Amount, dto.Pos2Amount, dto.CashAmount, dto.Notes);
            await _context.SaveChangesAsync();
            return existing.Id;
        }

        var isDuplicate = await _context.DailySettlements
            .AnyAsync(d => d.SettlementDate == targetDate);

        if (isDuplicate)
            throw new InvalidOperationException("برای این تاریخ قبلاً تسویه ثبت شده است.");

        var newSettlement = new DailySettlement(
            targetDate,
            dto.Pos1Amount,
            dto.Pos2Amount,
            dto.CashAmount,
            dto.Notes
        );

        await _context.DailySettlements.AddAsync(newSettlement);
        await _context.SaveChangesAsync();
        return newSettlement.Id;
    }

    public async Task CloseAsync(Guid id)
    {
        var entity = await _context.DailySettlements.FindAsync(id);
        if (entity == null)
            throw new InvalidOperationException("تسویه مورد نظر یافت نشد.");

        entity.CloseSettlement();
        await _context.SaveChangesAsync();
    }

    public async Task ReopenAsync(Guid id)
    {
        var entity = await _context.DailySettlements.FindAsync(id);
        if (entity == null)
            throw new InvalidOperationException("تسویه مورد نظر یافت نشد.");

        entity.ReopenSettlement();
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _context.DailySettlements.FindAsync(id);
        if (entity == null)
            throw new InvalidOperationException("تسویه مورد نظر یافت نشد.");

        if (entity.Status != SettlementStatus.Open)
            throw new InvalidOperationException("فقط تسویه‌های باز قابل حذف هستند.");

        _context.DailySettlements.Remove(entity);
        await _context.SaveChangesAsync();
    }
}
