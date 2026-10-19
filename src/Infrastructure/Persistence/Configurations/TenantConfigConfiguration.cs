using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class TenantConfigConfiguration : IEntityTypeConfiguration<Domain.Entities.TenantConfig>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.TenantConfig> builder)
    {
        builder.ToTable("TenantConfigs");

        builder.HasKey(x => x.Id);

        // No navigation property to Tenant on purpose: Tenant has a global query
        // filter and this 1:1 dependent is never query-filtered, so a required
        // reference navigation would trigger EF's filter-interaction warning.
        builder.HasOne<Domain.Entities.Tenant>()
            .WithOne()
            .HasForeignKey<Domain.Entities.TenantConfig>(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TenantId)
            .IsUnique();

        builder.Property(x => x.MaintenanceMessage)
            .HasMaxLength(500);

        builder.Property(x => x.LoginEnabled)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.WelcomeEmailEnabled)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.MaintenanceMode)
            .HasDefaultValue(false)
            .IsRequired();
    }
}
