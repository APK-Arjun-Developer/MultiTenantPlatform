using Infrastructure.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Identity.Configurations;

public class ApplicationRoleConfiguration
    : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("Roles");

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.NormalizedName, x.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_Roles_NormalizedName_TenantId")
            .HasFilter("[NormalizedName] IS NOT NULL AND [DeletedAt] IS NULL");
    }
}