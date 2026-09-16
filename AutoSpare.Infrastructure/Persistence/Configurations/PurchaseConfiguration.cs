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

        // اصلاح نام Description به Notes
        builder.Property(p => p.Notes)
            .HasMaxLength(500);

        // نادیده گرفتن فیلد محاسباتی
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

        // تنظیم فیلد پشتیبان _items
        builder.Navigation(p => p.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
