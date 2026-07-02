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

        builder.HasIndex(x => new { x.Email, x.TenantId })
            .IsUnique()
            .HasFilter("[Email] IS NOT NULL AND [DeletedAt] IS NULL");

        // Override the default Identity UserNameIndex so soft-deleted users
        // don't occupy the slot (allows the same email to be re-used after deletion).
        builder.HasIndex(x => x.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName("UserNameIndex")
            .HasFilter("[NormalizedUserName] IS NOT NULL AND [DeletedAt] IS NULL");

        builder.Property(x => x.SystemRole)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(x => x.ProfileFile)
            .WithMany()
            .HasForeignKey(x => x.ProfileFileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}