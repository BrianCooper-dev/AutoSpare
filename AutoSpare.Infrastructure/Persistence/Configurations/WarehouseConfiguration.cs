using AutoSpare.Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoSpare.Infrastructure.Persistence.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(w => w.Code)
            .HasMaxLength(30);

        // جلوگیری از ثبت کدهای تکراری برای انبارها (در صورتی که کد پر شده باشد)
        builder.HasIndex(w => w.Code)
            .IsUnique()
            .HasFilter("[Code] IS NOT NULL");

        builder.Property(w => w.Address)
            .HasMaxLength(300);

        builder.Property(w => w.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // تنظیم دسترسی به فیلد پشتیبان برای موجودی‌های انبار
        builder.Navigation(w => w.Inventories)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // داده‌های اولیه (Seed Data) انبارهای پیش‌فرض با سازنده دو پارامتری/با Id
        builder.HasData(
            new Warehouse(Warehouse.ShopWarehouseId, "انبار مغازه", "SHOP-01", "فروشگاه مرکزی"),
            new Warehouse(Warehouse.HomeWarehouseId, "انبار منزل", "HOME-01", "انبار پشتیبان")
        );
    }
}

