using AutoSpare.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoSpare.Infrastructure.Persistence.Configurations;

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("SaleItems", t =>
        {
            t.HasCheckConstraint("CK_SaleItems_Quantity_Positive", "[Quantity] > 0");
            t.HasCheckConstraint("CK_SaleItems_UnitPrice_NonNegative", "[UnitPrice] >= 0");
            t.HasCheckConstraint("CK_SaleItems_UnitPurchasePrice_NonNegative", "[UnitPurchasePrice] >= 0");
        });

        builder.HasKey(si => si.Id);

        builder.Property(si => si.SaleId).IsRequired();
        builder.Property(si => si.ProductId).IsRequired();
        builder.Property(si => si.WarehouseId).IsRequired();
        builder.Property(si => si.Quantity).IsRequired();

        // اصلاح دقت اعشار برای مبالغ و قیمت‌ها جهت جلوگیری از Truncation و خطای مالی
        builder.Property(si => si.UnitPrice)
            .IsRequired()
            .HasPrecision(18, 4);

        builder.Property(si => si.UnitPurchasePrice)
            .IsRequired()
            .HasPrecision(18, 4);

        // پراپرتی محاسباتی ناشی از Quantity * UnitPrice نباید ستون فیزیکی شود
        builder.Ignore(si => si.TotalPrice);

        // رابطه با کالا
        builder.HasOne(si => si.Product)
            .WithMany()
            .HasForeignKey(si => si.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // رابطه با انبار
        builder.HasOne(si => si.Warehouse)
            .WithMany()
            .HasForeignKey(si => si.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // ایندکس‌ها
        builder.HasIndex(si => si.ProductId);
        builder.HasIndex(si => si.SaleId);
        builder.HasIndex(si => si.WarehouseId);

        // ایندکس ترکیبی برای جلوگیری از ردیف تکراریِ یک کالا از یک انبار در یک فاکتور
        builder.HasIndex(si => new { si.SaleId, si.ProductId, si.WarehouseId })
            .IsUnique();
    }
}
