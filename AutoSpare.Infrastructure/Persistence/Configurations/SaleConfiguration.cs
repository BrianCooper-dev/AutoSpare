using AutoSpare.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoSpare.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50);

        // ایندکس یکتا برای شماره فاکتور فروش
        builder.HasIndex(s => s.InvoiceNumber)
            .IsUnique();

        builder.Property(s => s.CustomerName)
            .IsRequired()
            .HasMaxLength(150);

        // ایندکس جهت جست‌وجوی سریع نام مشتری
        builder.HasIndex(s => s.CustomerName);

        builder.Property(s => s.SaleDate)
            .IsRequired();

        // ایندکس روی تاریخ فروش
        builder.HasIndex(s => s.SaleDate);

        builder.Property(s => s.Status)
            .IsRequired();

        // فیلد توضیحات
        builder.Property(s => s.Notes)
            .HasMaxLength(500);

        // انبار سربرگ (اختیاری است چون انبار برای هر سطر جداگانه مشخص می‌شود)
        builder.Property(s => s.WarehouseId)
            .IsRequired(false);

        // رابطه با انبار در سربرگ فاکتور
        builder.HasOne(s => s.Warehouse)
            .WithMany()
            .HasForeignKey(s => s.WarehouseId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // فیلد محاسباتی در دیتابیس ذخیره نمی‌شود
        builder.Ignore(s => s.TotalAmount);

        // رابطه یک به چند با اقلام فروش (SaleItems)
        builder.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        // تنظیم دسترسی مستقیم به فیلد پشتیبان _items
        builder.Navigation(s => s.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
