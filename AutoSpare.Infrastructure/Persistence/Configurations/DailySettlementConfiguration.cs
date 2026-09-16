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

        builder.Property(d => d.Pos1Amount).HasPrecision(18, 2);
        builder.Property(d => d.Pos2Amount).HasPrecision(18, 2);
        builder.Property(d => d.CashAmount).HasPrecision(18, 2);

        // پراپرتی محاسباتی که نباید در دیتابیس ستون داشته باشد
        builder.Ignore(d => d.TotalAmount);

        // اصلاح Note به Notes
        builder.Property(d => d.Notes)
            .HasMaxLength(300);
    }
}
