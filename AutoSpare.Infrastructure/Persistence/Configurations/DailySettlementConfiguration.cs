using AutoSpare.Domain.DailySettlements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoSpare.Infrastructure.Persistence.Configurations;

public class DailySettlementConfiguration : IEntityTypeConfiguration<DailySettlement>
{
    public void Configure(EntityTypeBuilder<DailySettlement> builder)
    {
        builder.ToTable("DailySettlements", t =>
        {
            t.HasCheckConstraint("CK_DailySettlements_Pos1Amount_NonNegative", "[Pos1Amount] >= 0");
            t.HasCheckConstraint("CK_DailySettlements_Pos2Amount_NonNegative", "[Pos2Amount] >= 0");
            t.HasCheckConstraint("CK_DailySettlements_CashAmount_NonNegative", "[CashAmount] >= 0");
        });

        builder.HasKey(d => d.Id);

        builder.Property(d => d.SettlementDate).IsRequired();

        // اصلاح دقت اعشار مبالغ تسویه
        builder.Property(d => d.Pos1Amount).HasPrecision(18, 2);
        builder.Property(d => d.Pos2Amount).HasPrecision(18, 2);
        builder.Property(d => d.CashAmount).HasPrecision(18, 2);

        builder.Property(d => d.Status).IsRequired();
        builder.Property(d => d.Notes).HasMaxLength(300);

        builder.Ignore(d => d.TotalAmount);

        builder.HasIndex(d => d.SettlementDate).IsUnique();
    }
}
