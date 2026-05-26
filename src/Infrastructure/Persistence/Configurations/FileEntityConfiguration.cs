using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class FileEntityConfiguration
    : IEntityTypeConfiguration<FileEntity>
{
    public void Configure(EntityTypeBuilder<FileEntity> builder)
    {
        builder.ToTable("Files");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OriginalName)
            .HasMaxLength(300);

        builder.Property(x => x.StoredName)
            .HasMaxLength(300);

        builder.Property(x => x.ContentType)
            .HasMaxLength(100);
    }
}