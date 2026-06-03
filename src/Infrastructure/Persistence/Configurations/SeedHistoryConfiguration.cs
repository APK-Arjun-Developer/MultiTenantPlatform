using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class SeedHistoryConfiguration : IEntityTypeConfiguration<SeedHistory>
{
    public void Configure(EntityTypeBuilder<SeedHistory> builder)
    {
        builder.ToTable("SeedHistory");

        builder.HasKey(x => x.SeedId);

        builder.Property(x => x.SeedId)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);
    }
}
