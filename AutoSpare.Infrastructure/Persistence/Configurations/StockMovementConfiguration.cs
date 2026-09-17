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

        builder.Property(m => m.Reason)
            .HasMaxLength(300);

        builder.Property(m => m.Reference)
            .HasMaxLength(100);

        builder.Property(m => m.PerformedBy)
            .HasMaxLength(150);

        builder.Property(m => m.OccurredAt)
            .IsRequired();

        // فیلد محاسباتی به دیتابیس نگاشت نمی‌شود
        builder.Ignore(m => m.IsInbound);

        // کاردکس کالا: فیلتر بر اساس کالا + مرتب‌سازی زمانی بدون Sort اضافه
        builder.HasIndex(m => new { m.ProductId, m.OccurredAt });

        // گزارش گردش یک انبار در بازه زمانی
        builder.HasIndex(m => new { m.WarehouseId, m.OccurredAt });

        // رابطه با موجودی (ایندکس FK به‌صورت خودکار توسط EF ساخته می‌شود)
        builder.HasOne(m => m.Inventory)
            .WithMany(i => i.Movements)
            .HasForeignKey(m => m.InventoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
