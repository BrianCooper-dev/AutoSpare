using AutoSpare.Application.DailySettlements.DTOs;
using AutoSpare.Domain.DailySettlements;
using AutoSpare.Infrastructure.Persistence;
using AutoSpare.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AutoSpare.Tests.Services;

public class DailySettlementServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<DailySettlementService>> _loggerMock;
    private readonly DailySettlementService _service;
    private readonly SqliteConnection _connection;

    public DailySettlementServiceTests()
    {
        // برای حفظ داده‌ها در حافظه SQLite نیاز به باز نگه داشتن کانکشن است
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _loggerMock = new Mock<ILogger<DailySettlementService>>();
        _service = new DailySettlementService(_context, _loggerMock.Object);
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
            Pos1Amount = -5_000_000,
            Pos2Amount = 10_000_000,
            CashAmount = 5_000_000
        };

        // Act & Assert
        Func<Task> act = async () => await _service.UpsertAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*مبلغ نمی‌تواند منفی باشد*");
    }

    [Fact]
    public async Task CreateDailySettlement_WithDuplicateDate_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var settlementDate = DateTime.Today;

        var firstSettlement = new DailySettlement(
            settlementDate,
            pos1Amount: 10_000_000,
            pos2Amount: 0,
            cashAmount: 5_000_000
        );
        await _context.DailySettlements.AddAsync(firstSettlement);
        await _context.SaveChangesAsync();

        var duplicateDto = new UpsertDailySettlementDto
        {
            SettlementDate = settlementDate,
            Pos1Amount = 20_000_000,
            Pos2Amount = 10_000_000,
            CashAmount = 5_000_000
        };

        // Act
        Func<Task> act = async () => await _service.UpsertAsync(duplicateDto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*برای این تاریخ قبلاً تسویه ثبت شده است*");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
