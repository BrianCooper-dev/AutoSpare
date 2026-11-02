using AutoSpare.Application.DailySettlements.DTOs;

namespace AutoSpare.Application.DailySettlements;

public interface IDailySettlementService
{
    Task<List<DailySettlementDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<DailySettlementDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<DailySettlementDto?> GetByDateAsync(
        DateTime date,
        CancellationToken cancellationToken = default);

    Task<decimal> GetSystemSalesTotalAsync(
        DateTime date,
        CancellationToken cancellationToken = default);

    Task<Guid> UpsertAsync(
        UpsertDailySettlementDto dto,
        CancellationToken cancellationToken = default);

    Task CloseAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task ReopenAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
