using AutoSpare.Domain.Brands;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoSpare.Infrastructure.Persistence.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(b => b.Name)
            .IsUnique();

        builder.Property(b => b.Country)
            .HasMaxLength(60);

        builder.Property(b => b.Description)
            .HasMaxLength(300);

        builder.Property(b => b.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // تنظیم دسترسی به فیلد پشتیبان برای کالکشن محصولات
        builder.Navigation(b => b.Products)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

