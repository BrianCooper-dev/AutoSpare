using AutoSpare.Domain.Inventories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoSpare.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.QuantityChange)
            .IsRequired();

        builder.Property(m => m.BalanceAfter)
            .IsRequired();

        builder.Property(m => m.Type)
            .IsRequired();

        // اصلاح Description به Reason
        builder.Property(m => m.Reason)
            .HasMaxLength(300);

        // اصلاح ReferenceNumber به Reference
        builder.Property(m => m.Reference)
            .HasMaxLength(100);

        builder.Property(m => m.PerformedBy)
            .HasMaxLength(150);

        builder.Property(m => m.OccurredAt)
            .IsRequired();

        // نادیده گرفتن پراپرتی محاسباتی
        builder.Ignore(m => m.IsInbound);
    }
}
