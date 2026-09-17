using AutoSpare.Domain.Inventories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoSpare.Infrastructure.Persistence.Configurations;

public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("Inventories");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Quantity)
            .IsRequired()
            .HasDefaultValue(0);

        // رابطه با محصول
        builder.HasOne(i => i.Product)
            .WithMany(p => p.Inventories)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // رابطه با انبار
        builder.HasOne(i => i.Warehouse)
            .WithMany(w => w.Inventories)
            .HasForeignKey(i => i.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // روابط با گردش کالا (Movements)
        builder.HasMany(i => i.Movements)
            .WithOne(m => m.Inventory)
            .HasForeignKey(m => m.InventoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // دسترسی EF Core به فیلد پشتیبان _movements
        builder.Navigation(i => i.Movements)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ایندکس ترکیبی یکتا: هر کالا در هر انبار دقیقا یک سطر دارد
        builder.HasIndex(i => new { i.ProductId, i.WarehouseId })
            .IsUnique();

        // ایندکس جهت لیست کردن سریع موجودی‌های یک انبار خاص
        builder.HasIndex(i => i.WarehouseId);
    }
}
