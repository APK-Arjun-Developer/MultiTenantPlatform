using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Domain.Entities.Tenant>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CreatedVia)
            .HasConversion<int>()
            .HasDefaultValue(CreatedVia.Direct)
            .IsRequired();

        builder.HasOne(x => x.ProfileFile)
            .WithMany()
            .HasForeignKey(x => x.ProfileFileId)
            .OnDelete(DeleteBehavior.SetNull);

        // Audit columns — no TenantId (Tenant is the root aggregate, not a tenant-scoped entity).
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.DeletedAt);
        builder.Property(x => x.DeletedBy);
    }
}