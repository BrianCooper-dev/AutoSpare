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

        // ایندکس روی تاریخ فروش (مطابق تسک)
        builder.HasIndex(s => s.SaleDate);

        builder.Property(s => s.Status)
            .IsRequired();

        // فیلد توضیحات با نام Notes
        builder.Property(s => s.Notes)
            .HasMaxLength(500);

        // فیلد محاسباتی نادیده گرفته می‌شود
        builder.Ignore(s => s.TotalAmount);

        // رابطه با انبار
        builder.HasOne(s => s.Warehouse)
            .WithMany()
            .HasForeignKey(s => s.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // رابطه با اقلام فروش
        builder.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        // تنظیم دسترسی مستقیم به فیلد پشتیبان _items
        builder.Navigation(s => s.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
