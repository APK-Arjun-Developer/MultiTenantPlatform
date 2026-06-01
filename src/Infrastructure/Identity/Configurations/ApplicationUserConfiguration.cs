using Infrastructure.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Identity.Configurations;

public class ApplicationUserConfiguration
    : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");

        builder.Property(x => x.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.Email,
            x.TenantId
        }).IsUnique();
    }
}