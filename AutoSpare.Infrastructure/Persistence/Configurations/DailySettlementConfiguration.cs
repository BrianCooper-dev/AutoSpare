using AutoSpare.Domain.DailySettlements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoSpare.Infrastructure.Persistence.Configurations;

public class DailySettlementConfiguration : IEntityTypeConfiguration<DailySettlement>
{
    public void Configure(EntityTypeBuilder<DailySettlement> builder)
    {
        builder.ToTable("DailySettlements");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.SettlementDate)
            .IsRequired();

        builder.Property(d => d.Pos1Amount).HasPrecision(18, 2);
        builder.Property(d => d.Pos2Amount).HasPrecision(18, 2);
        builder.Property(d => d.CashAmount).HasPrecision(18, 2);

        builder.Property(d => d.Status)
            .IsRequired();

        builder.Property(d => d.Notes)
            .HasMaxLength(300);

        // پراپرتی محاسباتی
        builder.Ignore(d => d.TotalAmount);

        // ایندکس یکتا برای تضمین عدم ثبت بیش از یک تسویه در هر روز تقویمی
        builder.HasIndex(d => d.SettlementDate)
            .IsUnique();
    }
}
