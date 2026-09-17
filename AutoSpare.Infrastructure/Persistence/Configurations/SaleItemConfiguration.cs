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
        });

        builder.HasKey(si => si.Id);

        builder.Property(si => si.SaleId)
            .IsRequired();

        builder.Property(si => si.ProductId)
            .IsRequired();

        builder.Property(si => si.Quantity)
            .IsRequired();

        builder.Property(si => si.UnitPrice)
            .IsRequired()
            .HasPrecision(18, 2);

        // پراپرتی محاسباتی ناشی از Quantity * UnitPrice نباید ستون فیزیکی شود
        builder.Ignore(si => si.TotalPrice);

        // رابطه با کالا
        builder.HasOne(si => si.Product)
            .WithMany()
            .HasForeignKey(si => si.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // ایندکس جهت جوین و گزارش‌گیری روی کالاهای فروخته‌شده
        builder.HasIndex(si => si.ProductId);

        // ایندکس جهت بارگذاری سریع اقلام یک فاکتور فروش
        builder.HasIndex(si => si.SaleId);
    }
}
