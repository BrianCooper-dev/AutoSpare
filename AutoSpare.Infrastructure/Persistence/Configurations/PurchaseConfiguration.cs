using AutoSpare.Domain.Purchases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoSpare.Infrastructure.Persistence.Configurations;

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("Purchases");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.PurchaseDate)
            .IsRequired();

        builder.Property(p => p.Status)
            .IsRequired();

        builder.Property(p => p.Notes)
            .HasMaxLength(500);

        // پراپرتی محاسباتی نادیده گرفته می‌شود
        builder.Ignore(p => p.TotalAmount);

        // رابطه با تأمین‌کننده
        builder.HasOne(p => p.Supplier)
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        // رابطه با انبار
        builder.HasOne(p => p.Warehouse)
            .WithMany()
            .HasForeignKey(p => p.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // رابطه با اقلام فاکتور خرید
        builder.HasMany(p => p.Items)
            .WithOne()
            .HasForeignKey(i => i.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        // اتصال به فیلد پشتیبان دامین جهت رعایت Encapsulation
        builder.Navigation(p => p.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // تضمین یکتایی و جستجوی سریع بر اساس شماره فاکتور
        builder.HasIndex(p => p.InvoiceNumber)
            .IsUnique();

        // ایندکس ترکیبی: پوشش‌دهنده گزارش‌های زمانی و صورت‌حساب‌های دوره‌ای تأمین‌کننده
        builder.HasIndex(p => new { p.PurchaseDate, p.SupplierId });
    }
}
