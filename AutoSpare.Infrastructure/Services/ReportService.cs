using System.Globalization;
using AutoSpare.Application.Reports;
using AutoSpare.Application.Reports.DTOs;
using AutoSpare.Application.Reports.Enums;
using AutoSpare.Domain.Purchases.Enums;
using AutoSpare.Domain.Sales.Enums;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;
    private static readonly TimeSpan IranOffset = TimeSpan.FromHours(3.5);

    public ReportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var persianCalendar = new PersianCalendar();
        var nowUtc = DateTime.UtcNow;
        var nowIran = nowUtc.Add(IranOffset);

        // ۱. محاسبه بازه امروز بر اساس افست ایران
        var todayStartIran = nowIran.Date;
        var todayEndIran = todayStartIran.AddDays(1);

        var todayStartUtc = DateTime.SpecifyKind(todayStartIran - IranOffset, DateTimeKind.Utc);
        var todayEndUtc = DateTime.SpecifyKind(todayEndIran - IranOffset, DateTimeKind.Utc);

        // ۲. محاسبه بازه ماه جاری شمسی
        int pYear = persianCalendar.GetYear(nowIran);
        int pMonth = persianCalendar.GetMonth(nowIran);

        var monthStartIran = persianCalendar.ToDateTime(pYear, pMonth, 1, 0, 0, 0, 0);
        int daysInMonth = persianCalendar.GetDaysInMonth(pYear, pMonth);
        var monthEndIran = monthStartIran.AddDays(daysInMonth);

        var monthStartUtc = DateTime.SpecifyKind(monthStartIran - IranOffset, DateTimeKind.Utc);
        var monthEndUtc = DateTime.SpecifyKind(monthEndIran - IranOffset, DateTimeKind.Utc);

        // کوئری فروش‌های نهایی‌شده
        var baseSalesQuery = _context.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed);

        var todaySalesData = await baseSalesQuery
            .Where(s => s.SaleDate >= todayStartUtc && s.SaleDate < todayEndUtc)
            .Select(s => new
            {
                TotalAmount = s.Items.Sum(i => i.Quantity * i.UnitPrice),
                TotalCost = s.Items.Sum(i => i.Quantity * i.UnitPurchasePrice)
            })
            .ToListAsync(cancellationToken);

        var monthSalesData = await baseSalesQuery
            .Where(s => s.SaleDate >= monthStartUtc && s.SaleDate < monthEndUtc)
            .Select(s => new
            {
                TotalAmount = s.Items.Sum(i => i.Quantity * i.UnitPrice),
                TotalCost = s.Items.Sum(i => i.Quantity * i.UnitPurchasePrice)
            })
            .ToListAsync(cancellationToken);

        // کوئری خریدهای نهایی‌شده
        var basePurchasesQuery = _context.Purchases
            .AsNoTracking()
            .Where(p => p.Status == PurchaseStatus.Completed);

        var todayPurchases = await basePurchasesQuery
            .Where(p => p.PurchaseDate >= todayStartUtc && p.PurchaseDate < todayEndUtc)
            .Select(p => p.Items.Sum(i => i.Quantity * i.UnitPrice))
            .ToListAsync(cancellationToken);

        var monthPurchases = await basePurchasesQuery
            .Where(p => p.PurchaseDate >= monthStartUtc && p.PurchaseDate < monthEndUtc)
            .Select(p => p.Items.Sum(i => i.Quantity * i.UnitPrice))
            .ToListAsync(cancellationToken);

        // آمار موجودی و ارزش انبار
        var inventoryData = await _context.Inventories
            .AsNoTracking()
            .Where(inv => inv.Quantity > 0 && inv.Product != null)
            .Select(inv => new
            {
                inv.Quantity,
                PurchaseValue = inv.Quantity * inv.Product!.PurchasePrice,
                SaleValue = inv.Quantity * inv.Product!.SalePrice
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
            TotalInventoryPurchaseValue = inventoryData.Sum(x => x.PurchaseValue),
            TotalInventorySaleValue = inventoryData.Sum(x => x.SaleValue)
        };
    }

    public async Task<List<SalesSummaryReportDto>> GetDailySalesSummaryAsync(ReportFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var rawSales = await GetFilteredBaseSalesAsync(filter, cancellationToken);
        var persianCalendar = new PersianCalendar();

        var dailyData = rawSales
            .GroupBy(x =>
            {
                var iranDate = x.SaleDate.Add(IranOffset).Date;
                return iranDate;
            })
            .Select(g =>
            {
                var d = g.Key;
                var py = persianCalendar.GetYear(d);
                var pm = persianCalendar.GetMonth(d);
                var pd = persianCalendar.GetDayOfMonth(d);

                return new SalesSummaryReportDto
                {
                    Date = d,
                    PeriodTitle = $"{py:0000}/{pm:00}/{pd:00}",
                    InvoiceCount = g.Count(),
                    TotalQuantitySold = g.Sum(x => x.TotalItems),
                    TotalSales = g.Sum(x => x.InvoiceTotal),
                    TotalCost = g.Sum(x => x.InvoiceCost),
                    TotalProfit = g.Sum(x => x.InvoiceTotal - x.InvoiceCost)
                };
            })
            .OrderByDescending(r => r.Date)
            .ToList();

        return dailyData;
    }

    public async Task<List<SalesSummaryReportDto>> GetWeeklySalesSummaryAsync(ReportFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var rawSales = await GetFilteredBaseSalesAsync(filter, cancellationToken);
        var persianCalendar = new PersianCalendar();

        var weeklyData = rawSales
            .GroupBy(x =>
            {
                var date = x.SaleDate.Add(IranOffset).Date;
                int diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Saturday) % 7;
                var weekStart = date.AddDays(-1 * diff);
                return weekStart;
            })
            .Select(g =>
            {
                var weekStart = g.Key;
                var weekEnd = weekStart.AddDays(6);
                var py = persianCalendar.GetYear(weekStart);
                var pm = persianCalendar.GetMonth(weekStart);
                var pd = persianCalendar.GetDayOfMonth(weekStart);
                var pey = persianCalendar.GetYear(weekEnd);
                var pem = persianCalendar.GetMonth(weekEnd);
                var ped = persianCalendar.GetDayOfMonth(weekEnd);

                return new SalesSummaryReportDto
                {
                    Date = weekStart,
                    PeriodTitle = $"هفته ({py:0000}/{pm:00}/{pd:00} الی {pey:0000}/{pem:00}/{ped:00})",
                    InvoiceCount = g.Count(),
                    TotalQuantitySold = g.Sum(x => x.TotalItems),
                    TotalSales = g.Sum(x => x.InvoiceTotal),
                    TotalCost = g.Sum(x => x.InvoiceCost),
                    TotalProfit = g.Sum(x => x.InvoiceTotal - x.InvoiceCost)
                };
            })
            .OrderByDescending(r => r.Date)
            .ToList();

        return weeklyData;
    }

    public async Task<List<SalesSummaryReportDto>> GetMonthlySalesSummaryAsync(ReportFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var rawSales = await GetFilteredBaseSalesAsync(filter, cancellationToken);
        var persianCalendar = new PersianCalendar();

        var monthNames = new[]
        {
            "", "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
            "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
        };

        var monthlyData = rawSales
            .GroupBy(x =>
            {
                var iranDate = x.SaleDate.Add(IranOffset);
                var py = persianCalendar.GetYear(iranDate);
                var pm = persianCalendar.GetMonth(iranDate);
                return new { Year = py, Month = pm };
            })
            .Select(g =>
            {
                var monthName = (g.Key.Month >= 1 && g.Key.Month <= 12)
                    ? monthNames[g.Key.Month]
                    : $"ماه {g.Key.Month}";

                var repDate = persianCalendar.ToDateTime(g.Key.Year, g.Key.Month, 1, 0, 0, 0, 0);

                return new SalesSummaryReportDto
                {
                    Date = repDate,
                    PeriodTitle = $"{monthName} {g.Key.Year}",
                    InvoiceCount = g.Count(),
                    TotalQuantitySold = g.Sum(x => x.TotalItems),
                    TotalSales = g.Sum(x => x.InvoiceTotal),
                    TotalCost = g.Sum(x => x.InvoiceCost),
                    TotalProfit = g.Sum(x => x.InvoiceTotal - x.InvoiceCost)
                };
            })
            .OrderByDescending(r => r.Date)
            .ToList();

        return monthlyData;
    }

    private async Task<List<RawSaleRecord>> GetFilteredBaseSalesAsync(ReportFilterDto filter,
        CancellationToken cancellationToken)
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

        return await query
            .Select(s => new RawSaleRecord
            {
                SaleDate = s.SaleDate,
                TotalItems = s.Items.Sum(i => i.Quantity),
                InvoiceTotal = s.Items.Sum(i => i.Quantity * i.UnitPrice),
                InvoiceCost = s.Items.Sum(i => i.Quantity * i.UnitPurchasePrice)
            })
            .ToListAsync(cancellationToken);
    }

    private sealed class RawSaleRecord
    {
        public DateTime SaleDate { get; set; }
        public int TotalItems { get; set; }
        public decimal InvoiceTotal { get; set; }
        public decimal InvoiceCost { get; set; }
    }

    public async Task<List<TopSellingProductDto>> GetTopSellingProductsAsync(int count,
        TopSellingSortMode sortMode = TopSellingSortMode.ByQuantity,
        ReportFilterDto? filter = null,
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

        var groupedQuery = itemsQuery
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
            });

        groupedQuery = sortMode == TopSellingSortMode.BySalesAmount
            ? groupedQuery.OrderByDescending(x => x.TotalSalesAmount)
            : groupedQuery.OrderByDescending(x => x.TotalQuantitySold);

        return await groupedQuery
            .Take(count)
            .ToListAsync(cancellationToken);
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

    public async Task<List<ProfitItemReportDto>> GetProfitReportAsync(ReportFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var salesQuery = _context.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed);

        if (filter.FromDate.HasValue)
            salesQuery = salesQuery.Where(s => s.SaleDate >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            salesQuery = salesQuery.Where(s => s.SaleDate <= filter.ToDate.Value);

        if (filter.WarehouseId.HasValue)
        {
            salesQuery = salesQuery.Where(s =>
                s.WarehouseId == filter.WarehouseId.Value ||
                s.Items.Any(i => i.WarehouseId == filter.WarehouseId.Value));
        }

        var itemsQuery = salesQuery.SelectMany(s => s.Items.Select(item => new
        {
            SaleId = s.Id,
            InvoiceNumber = s.InvoiceNumber,
            SaleDate = s.SaleDate,
            ProductName = item.Product != null ? item.Product.Name : string.Empty,
            ProductCode = item.Product != null ? item.Product.InternalCode : string.Empty,
            WarehouseName = item.Warehouse != null ? item.Warehouse.Name : "-",
            Quantity = item.Quantity,
            UnitCost = item.UnitPurchasePrice,
            UnitPrice = item.UnitPrice,
            ItemWarehouseId = item.WarehouseId,
            SaleWarehouseId = s.WarehouseId
        }));

        if (filter.WarehouseId.HasValue)
        {
            itemsQuery = itemsQuery.Where(x =>
                x.ItemWarehouseId == filter.WarehouseId.Value ||
                x.SaleWarehouseId == filter.WarehouseId.Value);
        }

        var result = await itemsQuery
            .OrderByDescending(x => x.SaleDate)
            .Select(x => new ProfitItemReportDto
            {
                SaleId = x.SaleId,
                InvoiceNumber = x.InvoiceNumber,
                SaleDate = x.SaleDate,
                ProductName = x.ProductName,
                ProductCode = x.ProductCode,
                WarehouseName = x.WarehouseName,
                Quantity = x.Quantity,
                UnitCost = x.UnitCost,
                UnitPrice = x.UnitPrice
            })
            .ToListAsync(cancellationToken);

        return result;
    }
}
