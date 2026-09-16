using AutoSpare.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoSpare.Infrastructure.Persistence.Configurations;

public class PriceChangeHistoryConfiguration : IEntityTypeConfiguration<PriceChangeHistory>
{
    public void Configure(EntityTypeBuilder<PriceChangeHistory> builder)
    {
        builder.ToTable("PriceChangeHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.OldPurchasePrice).HasPrecision(18, 2);
        builder.Property(h => h.NewPurchasePrice).HasPrecision(18, 2);
        builder.Property(h => h.OldSalePrice).HasPrecision(18, 2);
        builder.Property(h => h.NewSalePrice).HasPrecision(18, 2);

        // اصلاح نام پراپرتی به Reason
        builder.Property(h => h.Reason)
            .HasMaxLength(300);

        // نادیده گرفتن ویژگی‌های محاسباتی
        builder.Ignore(h => h.SalePriceDifference);
        builder.Ignore(h => h.SalePricePercentageChange);
    }
}
