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

        // رابطه با انبار پیش‌فرض (اختیاری)
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

        // دسترسی مستقیم به فیلد پشتیبان _inventories
        builder.Navigation(p => p.Inventories)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ۱. ایندکس یکتا روی کد کالا (مانع ایجاد کد تکراری)
        builder.HasIndex(p => p.InternalCode)
            .IsUnique();

        // ۲. ایندکس ترکیبی نام و مدل خودرو (پوشش‌دهنده سرچ نام کالا و سرچ همزمان نام + مدل)
        builder.HasIndex(p => new { p.Name, p.Model });

        // ۳. ایندکس مجزا روی مدل خودرو (جهت فیلتر لیست فقط بر اساس مدل ماشین)
        builder.HasIndex(p => p.Model);

        // ۴. ایندکس روی کلید خارجی برند جهت فیلتر و جوین‌های سریع
        builder.HasIndex(p => p.BrandId);

        // ۵. ایندکس روی کلید خارجی دسته‌بندی جهت فیلتر و جوین‌های سریع
        builder.HasIndex(p => p.CategoryId);
    }
}
