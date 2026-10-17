using AutoSpare.Application.DailySettlements.DTOs;
using AutoSpare.Domain.DailySettlements;
using AutoSpare.Infrastructure.Persistence;
using AutoSpare.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Tests.Services;

public class DailySettlementServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DailySettlementService _service;

    public DailySettlementServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _service = new DailySettlementService(_context);
    }

    [Fact]
    public async Task CreateDailySettlement_WithValidAmounts_ShouldSucceedAndCalculateTotalCorrectly()
    {
        // Arrange
        var settlementDate = DateTime.Today;
        var dto = new UpsertDailySettlementDto
        {
            SettlementDate = settlementDate,
            Pos1Amount = 50_000_000,
            Pos2Amount = 30_000_000,
            CashAmount = 20_000_000,
            Notes = "تسویه روز عادی"
        };

        // Act
        var settlementId = await _service.UpsertAsync(dto);

        // Assert
        settlementId.Should().NotBeEmpty();
        var savedSettlement = await _context.DailySettlements.FindAsync(settlementId);
        savedSettlement.Should().NotBeNull();
        savedSettlement!.Pos1Amount.Should().Be(50_000_000);
        savedSettlement.Pos2Amount.Should().Be(30_000_000);
        savedSettlement.CashAmount.Should().Be(20_000_000);
        savedSettlement.TotalAmount.Should().Be(100_000_000);
    }

    [Fact]
    public async Task CreateDailySettlement_WithNegativeAmount_ShouldThrowArgumentException()
    {
        // Arrange
        var dto = new UpsertDailySettlementDto
        {
            SettlementDate = DateTime.Today,
            Pos1Amount = -5_000_000, // مبلغ منفی غیرمجاز
            Pos2Amount = 10_000_000,
            CashAmount = 5_000_000
        };

        // Act & Assert
        // مبالغ منفی توسط Constructor کلاس Domain (DailySettlement) با ArgumentException رد می‌شوند
        var act = async () => await _service.UpsertAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*مبلغ نمی‌تواند منفی باشد*");
    }

    [Fact]
    public async Task CreateDailySettlement_WithDuplicateDate_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var settlementDate = DateTime.Today;

        // ثبت تسویه اول برای تاریخ مشخص
        var firstSettlement = new DailySettlement(
            settlementDate,
            pos1Amount: 10_000_000,
            pos2Amount: 0,
            cashAmount: 5_000_000
        );
        await _context.DailySettlements.AddAsync(firstSettlement);
        await _context.SaveChangesAsync();

        // تلاش برای ثبت تسویه دوم برای همان تاریخ
        var duplicateDto = new UpsertDailySettlementDto
        {
            SettlementDate = settlementDate,
            Pos1Amount = 20_000_000,
            Pos2Amount = 10_000_000,
            CashAmount = 5_000_000
        };

        // Act
        var act = async () => await _service.UpsertAsync(duplicateDto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*برای این تاریخ قبلاً تسویه ثبت شده است*");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
