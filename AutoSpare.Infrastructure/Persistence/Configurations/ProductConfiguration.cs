using AutoSpare.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoSpare.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.InternalCode)
            .IsRequired()
            .HasMaxLength(50);

        // اصلاح نام CarModel به Model
        builder.Property(p => p.Model)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.PurchasePrice)
            .HasPrecision(18, 2);

        builder.Property(p => p.SalePrice)
            .HasPrecision(18, 2);

        builder.Property(p => p.ImagePath)
            .HasMaxLength(500);

        builder.Property(p => p.Status)
            .IsRequired();

        // رابطه با دسته‌بندی
        builder.HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // رابطه با برند
        builder.HasOne(p => p.Brand)
            .WithMany()
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        // رابطه اختیاری با انبار پیش‌فرض
        builder.HasOne(p => p.DefaultWarehouse)
            .WithMany()
            .HasForeignKey(p => p.DefaultWarehouseId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // رابطه با موجودی‌ها
        builder.HasMany(p => p.Inventories)
            .WithOne(i => i.Product)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // تنظیم دسترسی به فیلد پشتیبان _inventories
        builder.Navigation(p => p.Inventories)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
