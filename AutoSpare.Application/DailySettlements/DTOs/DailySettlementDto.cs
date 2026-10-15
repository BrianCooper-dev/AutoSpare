namespace AutoSpare.Application.DailySettlements.DTOs;

public class DailySettlementDto
{
    public Guid Id { get; set; }
    public DateTime SettlementDate { get; set; }
    public decimal Pos1Amount { get; set; }
    public decimal Pos2Amount { get; set; }
    public decimal CashAmount { get; set; }
    public decimal TotalAmount => Pos1Amount + Pos2Amount + CashAmount;
    public int Status { get; set; }
    public string StatusTitle => Status == 1 ? "باز" : "بسته شده";
    public string? Notes { get; set; }
}
