using AutoSpare.Application.DailySettlements.DTOs;

namespace AutoSpare.Application.DailySettlements;

public interface IDailySettlementService
{
    Task<List<DailySettlementDto>> GetAllAsync();
    Task<DailySettlementDto?> GetByIdAsync(Guid id);
    Task<DailySettlementDto?> GetByDateAsync(DateTime date);
    Task<decimal> GetSystemSalesTotalAsync(DateTime date);
    Task<Guid> UpsertAsync(UpsertDailySettlementDto dto);
    Task CloseAsync(Guid id);
    Task ReopenAsync(Guid id);
    Task DeleteAsync(Guid id);
}
