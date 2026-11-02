using AutoSpare.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoSpare.Infrastructure.Persistence.Configurations;

public class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> b)
    {
        b.ToTable("Settings");

        b.HasKey(x => x.Id);

        b.Property(x => x.StoreName)
            .IsRequired()
            .HasMaxLength(150);

        b.Property(x => x.Phone)
            .HasMaxLength(50);

        b.Property(x => x.Mobile)
            .HasMaxLength(50);

        b.Property(x => x.Address)
            .HasMaxLength(400);

        b.Property(x => x.FooterNote)
            .HasMaxLength(500);
    }
}
