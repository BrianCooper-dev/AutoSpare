using AutoSpare.Application.Reports.DTOs;

namespace AutoSpare.Application.Reports;

public interface IReportService
{
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);

    Task<List<SalesSummaryReportDto>> GetDailySalesSummaryAsync(ReportFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<List<SalesSummaryReportDto>> GetWeeklySalesSummaryAsync(ReportFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<List<SalesSummaryReportDto>> GetMonthlySalesSummaryAsync(ReportFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<List<TopSellingProductDto>> GetTopSellingProductsAsync(int count, ReportFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    Task<List<InventoryValuationDto>> GetInventoryValuationAsync(Guid? warehouseId = null,
        CancellationToken cancellationToken = default);

    Task<List<ProfitItemReportDto>> GetProfitReportAsync(ReportFilterDto filter,
        CancellationToken cancellationToken = default);
}
