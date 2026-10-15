using System.ComponentModel.DataAnnotations;

namespace AutoSpare.Application.DailySettlements.DTOs;

public class UpsertDailySettlementDto
{
    [Required(ErrorMessage = "تاریخ تسویه الزامی است.")]
    public DateTime? SettlementDate { get; set; } = DateTime.Today;

    [Range(0, double.MaxValue, ErrorMessage = "مبلغ نمی‌تواند منفی باشد.")]
    public decimal Pos1Amount { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "مبلغ نمی‌تواند منفی باشد.")]
    public decimal Pos2Amount { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "مبلغ نمی‌تواند منفی باشد.")]
    public decimal CashAmount { get; set; }

    [StringLength(300, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۳۰۰ کاراکتر باشد.")]
    public string? Notes { get; set; }
}
