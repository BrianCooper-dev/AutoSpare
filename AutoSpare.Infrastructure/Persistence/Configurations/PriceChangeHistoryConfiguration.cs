using AutoSpare.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoSpare.Infrastructure.Persistence.Configurations;

public class PriceChangeHistoryConfiguration : IEntityTypeConfiguration<PriceChangeHistory>
{
    public void Configure(EntityTypeBuilder<PriceChangeHistory> builder)
    {
        builder.ToTable("PriceChangeHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.OldPurchasePrice).HasPrecision(18, 2);
        builder.Property(h => h.NewPurchasePrice).HasPrecision(18, 2);
        builder.Property(h => h.OldSalePrice).HasPrecision(18, 2);
        builder.Property(h => h.NewSalePrice).HasPrecision(18, 2);

        builder.Property(h => h.ChangeType)
            .IsRequired();

        builder.Property(h => h.ChangeDate)
            .IsRequired();

        builder.Property(h => h.Reason)
            .HasMaxLength(300);

        // پراپرتی‌های محاسباتی دامین نباید ستون دیتابیس شوند
        builder.Ignore(h => h.SalePriceDifference);
        builder.Ignore(h => h.SalePricePercentageChange);

        // رابطه با کالا (جلوگیری از حذف زنجیره‌ای ناخواسته با Restrict)
        builder.HasOne(h => h.Product)
            .WithMany()
            .HasForeignKey(h => h.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // ایندکس ترکیبی برای گرفتن تاریخچه تغییرات یک کالای خاص مرتب‌شده بر اساس تاریخ
        builder.HasIndex(h => new { h.ProductId, h.ChangeDate });

        // ایندکس مجزا برای فیلتر و گزارش‌های کلی تغییرات قیمت در بازه زمانی خاص
        builder.HasIndex(h => h.ChangeDate);
    }
}
