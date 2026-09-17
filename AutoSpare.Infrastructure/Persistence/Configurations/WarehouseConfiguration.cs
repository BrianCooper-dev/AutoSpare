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

        // داده‌های اولیه با مقادیر ثابت و قطعی (بدون وابستگی به DateTime.UtcNow لحظه‌ای)
        var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new
            {
                Id = Warehouse.ShopWarehouseId,
                Name = "انبار مغازه",
                Code = "SHOP-01",
                Address = "فروشگاه مرکزی",
                IsActive = true,
                IsDeleted = false,
                CreatedAt = seedDate
            },
            new
            {
                Id = Warehouse.HomeWarehouseId,
                Name = "انبار منزل",
                Code = "HOME-01",
                Address = "انبار پشتیبان",
                IsActive = true,
                IsDeleted = false,
                CreatedAt = seedDate
            }
        );
    }
}
