using Domain.Entities;
using Infrastructure.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Addresses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Line1)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Line2)
            .HasMaxLength(200);

        builder.Property(x => x.City)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.State)
            .HasMaxLength(100);

        builder.Property(x => x.PostalCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Country)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithOne(u => u.Address)
            .HasForeignKey<Address>(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Domain.Entities.Tenant>()
            .WithOne(t => t.Address)
            .HasForeignKey<Address>(a => a.OwnerTenantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");

        builder.HasIndex(x => x.OwnerTenantId)
            .IsUnique()
            .HasFilter("[OwnerTenantId] IS NOT NULL");
    }
}
