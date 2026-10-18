using AutoSpare.Application.Reports;
using AutoSpare.Application.Reports.DTOs;
using AutoSpare.Domain.Purchases.Enums;
using AutoSpare.Domain.Sales.Enums;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;

    public ReportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var todayEnd = todayStart.AddDays(1);

        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        // ۱. کوئری Read-only فاکتورهای فروش نهایی شده
        var baseSalesQuery = _context.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed);

        // آمار فروش و سود امروز
        var todaySalesData = await baseSalesQuery
            .Where(s => s.SaleDate >= todayStart && s.SaleDate < todayEnd)
            .Select(s => new
            {
                TotalAmount = s.Items.Sum(i => i.Quantity * i.UnitPrice),
                TotalCost = s.Items.Sum(i => i.Quantity * i.UnitPurchasePrice)
            })
            .ToListAsync(cancellationToken);

        // آمار فروش و سود این ماه
        var monthSalesData = await baseSalesQuery
            .Where(s => s.SaleDate >= monthStart && s.SaleDate < monthEnd)
            .Select(s => new
            {
                TotalAmount = s.Items.Sum(i => i.Quantity * i.UnitPrice),
                TotalCost = s.Items.Sum(i => i.Quantity * i.UnitPurchasePrice)
            })
            .ToListAsync(cancellationToken);

        // ۲. کوئری خریدهای نهایی شده
        var basePurchasesQuery = _context.Purchases
            .AsNoTracking()
            .Where(p => p.Status == PurchaseStatus.Completed);

        var todayPurchases = await basePurchasesQuery
            .Where(p => p.PurchaseDate >= todayStart && p.PurchaseDate < todayEnd)
            .Select(p => p.Items.Sum(i => i.Quantity * i.UnitPrice))
            .ToListAsync(cancellationToken);

        var monthPurchases = await basePurchasesQuery
            .Where(p => p.PurchaseDate >= monthStart && p.PurchaseDate < monthEnd)
            .Select(p => p.Items.Sum(i => i.Quantity * i.UnitPrice))
            .ToListAsync(cancellationToken);

        // ۳. کوئری موجودی و ارزش ریالی انبار
        var inventoryData = await _context.Inventories
            .AsNoTracking()
            .Where(inv => inv.Quantity > 0 && inv.Product != null)
            .Select(inv => new
            {
                inv.Quantity,
                PurchasePrice = inv.Product!.PurchasePrice,
                SalePrice = inv.Product!.SalePrice
            })
            .ToListAsync(cancellationToken);

        return new DashboardSummaryDto
        {
            TodaySalesAmount = todaySalesData.Sum(x => x.TotalAmount),
            TodayProfitAmount = todaySalesData.Sum(x => x.TotalAmount - x.TotalCost),
            TodayCompletedSalesCount = todaySalesData.Count,

            MonthSalesAmount = monthSalesData.Sum(x => x.TotalAmount),
            MonthProfitAmount = monthSalesData.Sum(x => x.TotalAmount - x.TotalCost),
            MonthCompletedSalesCount = monthSalesData.Count,

            TodayPurchasesAmount = todayPurchases.Sum(),
            MonthPurchasesAmount = monthPurchases.Sum(),

            TotalStockItemsCount = inventoryData.Sum(x => x.Quantity),
            TotalInventoryPurchaseValue = inventoryData.Sum(x => x.Quantity * x.PurchasePrice),
            TotalInventorySaleValue = inventoryData.Sum(x => x.Quantity * x.SalePrice)
        };
    }

    public async Task<List<SalesSummaryReportDto>> GetDailySalesSummaryAsync(ReportFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed);

        if (filter.FromDate.HasValue)
            query = query.Where(s => s.SaleDate >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(s => s.SaleDate <= filter.ToDate.Value);

        if (filter.WarehouseId.HasValue)
            query = query.Where(s =>
                s.WarehouseId == filter.WarehouseId.Value ||
                s.Items.Any(i => i.WarehouseId == filter.WarehouseId.Value));

        var dailyData = await query
            .Select(s => new
            {
                Date = s.SaleDate.Date,
                InvoiceTotal = s.Items.Sum(i => i.Quantity * i.UnitPrice),
                InvoiceCost = s.Items.Sum(i => i.Quantity * i.UnitPurchasePrice)
            })
            .GroupBy(x => x.Date)
            .Select(g => new SalesSummaryReportDto
            {
                Date = g.Key,
                InvoiceCount = g.Count(),
                TotalSales = g.Sum(x => x.InvoiceTotal),
                TotalCost = g.Sum(x => x.InvoiceCost),
                TotalProfit = g.Sum(x => x.InvoiceTotal - x.InvoiceCost)
            })
            .OrderByDescending(r => r.Date)
            .ToListAsync(cancellationToken);

        return dailyData;
    }

    public async Task<List<TopSellingProductDto>> GetTopSellingProductsAsync(int count, ReportFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var itemsQuery = _context.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .SelectMany(s => s.Items.Select(item => new
            {
                SaleDate = s.SaleDate,
                item.ProductId,
                ProductName = item.Product != null ? item.Product.Name : string.Empty,
                InternalCode = item.Product != null ? item.Product.InternalCode : string.Empty,
                BrandName = item.Product != null && item.Product.Brand != null ? item.Product.Brand.Name : "-",
                item.WarehouseId,
                item.Quantity,
                item.UnitPrice,
                item.UnitPurchasePrice
            }));

        if (filter != null)
        {
            if (filter.FromDate.HasValue)
                itemsQuery = itemsQuery.Where(x => x.SaleDate >= filter.FromDate.Value);

            if (filter.ToDate.HasValue)
                itemsQuery = itemsQuery.Where(x => x.SaleDate <= filter.ToDate.Value);

            if (filter.WarehouseId.HasValue)
                itemsQuery = itemsQuery.Where(x => x.WarehouseId == filter.WarehouseId.Value);
        }

        var grouped = await itemsQuery
            .GroupBy(x => new { x.ProductId, x.ProductName, x.InternalCode, x.BrandName })
            .Select(g => new TopSellingProductDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                InternalCode = g.Key.InternalCode,
                BrandName = g.Key.BrandName,
                TotalQuantitySold = g.Sum(i => i.Quantity),
                TotalSalesAmount = g.Sum(i => i.Quantity * i.UnitPrice),
                TotalProfitAmount = g.Sum(i => (i.Quantity * i.UnitPrice) - (i.Quantity * i.UnitPurchasePrice))
            })
            .OrderByDescending(x => x.TotalQuantitySold)
            .Take(count)
            .ToListAsync(cancellationToken);

        return grouped;
    }

    public async Task<List<InventoryValuationDto>> GetInventoryValuationAsync(Guid? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Inventories
            .AsNoTracking()
            .Where(i => i.Quantity > 0);

        if (warehouseId.HasValue)
            query = query.Where(i => i.WarehouseId == warehouseId.Value);

        return await query
            .Select(i => new InventoryValuationDto
            {
                ProductId = i.ProductId,
                ProductName = i.Product != null ? i.Product.Name : string.Empty,
                InternalCode = i.Product != null ? i.Product.InternalCode : string.Empty,
                WarehouseName = i.Warehouse != null ? i.Warehouse.Name : "-",
                CurrentStock = i.Quantity,
                PurchasePrice = i.Product != null ? i.Product.PurchasePrice : 0m,
                SalePrice = i.Product != null ? i.Product.SalePrice : 0m
            })
            .OrderByDescending(x => x.CurrentStock * x.PurchasePrice)
            .ToListAsync(cancellationToken);
    }
}
