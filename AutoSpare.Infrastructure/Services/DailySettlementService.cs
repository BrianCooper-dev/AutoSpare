using AutoSpare.Application.DailySettlements;
using AutoSpare.Application.DailySettlements.DTOs;
using AutoSpare.Domain.DailySettlements;
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
        return await _context.DailySettlements
            .OrderByDescending(d => d.SettlementDate)
            .Select(d => new DailySettlementDto
            {
                Id = d.Id,
                SettlementDate = d.SettlementDate,
                Pos1Amount = d.Pos1Amount,
                Pos2Amount = d.Pos2Amount,
                CashAmount = d.CashAmount,
                Status = (int)d.Status,
                Notes = d.Notes
            })
            .ToListAsync();
    }

    public async Task<DailySettlementDto?> GetByIdAsync(Guid id)
    {
        var entity = await _context.DailySettlements.FindAsync(id);
        if (entity == null) return null;

        return new DailySettlementDto
        {
            Id = entity.Id,
            SettlementDate = entity.SettlementDate,
            Pos1Amount = entity.Pos1Amount,
            Pos2Amount = entity.Pos2Amount,
            CashAmount = entity.CashAmount,
            Status = (int)entity.Status,
            Notes = entity.Notes
        };
    }

    public async Task<DailySettlementDto?> GetByDateAsync(DateTime date)
    {
        var targetDate = date.Date;
        var entity = await _context.DailySettlements
            .FirstOrDefaultAsync(d => d.SettlementDate == targetDate);

        if (entity == null) return null;

        return new DailySettlementDto
        {
            Id = entity.Id,
            SettlementDate = entity.SettlementDate,
            Pos1Amount = entity.Pos1Amount,
            Pos2Amount = entity.Pos2Amount,
            CashAmount = entity.CashAmount,
            Status = (int)entity.Status,
            Notes = entity.Notes
        };
    }

    public async Task<Guid> UpsertAsync(UpsertDailySettlementDto dto)
    {
        var targetDate = (dto.SettlementDate ?? DateTime.Today).Date;
        var existing = await _context.DailySettlements
            .FirstOrDefaultAsync(d => d.SettlementDate == targetDate);

        if (existing != null)
        {
            existing.UpdateAmounts(dto.Pos1Amount, dto.Pos2Amount, dto.CashAmount, dto.Notes);
            _context.DailySettlements.Update(existing);
            await _context.SaveChangesAsync();
            return existing.Id;
        }
        else
        {
            var settlement = new DailySettlement(
                targetDate,
                dto.Pos1Amount,
                dto.Pos2Amount,
                dto.CashAmount,
                dto.Notes
            );
            await _context.DailySettlements.AddAsync(settlement);
            await _context.SaveChangesAsync();
            return settlement.Id;
        }
    }

    public async Task CloseAsync(Guid id)
    {
        var entity = await _context.DailySettlements.FindAsync(id)
                     ?? throw new InvalidOperationException("تسویه روزانه مورد نظر یافت نشد.");

        entity.CloseSettlement();
        await _context.SaveChangesAsync();
    }

    public async Task ReopenAsync(Guid id)
    {
        var entity = await _context.DailySettlements.FindAsync(id)
                     ?? throw new InvalidOperationException("تسویه روزانه مورد نظر یافت نشد.");

        entity.ReopenSettlement();
        await _context.SaveChangesAsync();
    }
}
