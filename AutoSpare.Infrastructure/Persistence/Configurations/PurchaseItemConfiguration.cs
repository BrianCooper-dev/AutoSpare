using AutoSpare.Domain.Purchases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoSpare.Infrastructure.Persistence.Configurations;

public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        builder.ToTable("PurchaseItems", t =>
        {
            t.HasCheckConstraint("CK_PurchaseItems_Quantity_Positive", "[Quantity] > 0");
            t.HasCheckConstraint("CK_PurchaseItems_UnitPrice_NonNegative", "[UnitPrice] >= 0");
        });

        builder.HasKey(pi => pi.Id);

        builder.Property(pi => pi.PurchaseId).IsRequired();
        builder.Property(pi => pi.ProductId).IsRequired();
        builder.Property(pi => pi.Quantity).IsRequired();

        // اصلاح دقت اعشار خرید
        builder.Property(pi => pi.UnitPrice)
            .IsRequired()
            .HasPrecision(18, 4);

        // پراپرتی محاسباتی است و نباید به ستون فیزیکی نگاشت شود
        builder.Ignore(pi => pi.TotalPrice);

        // رابطه با کالا
        builder.HasOne(pi => pi.Product)
            .WithMany()
            .HasForeignKey(pi => pi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // ایندکس‌ها
        builder.HasIndex(pi => pi.ProductId);
        builder.HasIndex(pi => pi.PurchaseId);
    }
}
