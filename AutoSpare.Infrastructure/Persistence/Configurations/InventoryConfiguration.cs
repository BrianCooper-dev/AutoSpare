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

        // روابط با گردش کالا (اصلاح Movements)
        builder.HasMany(i => i.Movements)
            .WithOne(m => m.Inventory)
            .HasForeignKey(m => m.InventoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // تنظیم دسترسی EF Core به فیلد پشتیبان
        builder.Navigation(i => i.Movements)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
