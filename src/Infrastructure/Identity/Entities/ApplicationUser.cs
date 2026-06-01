using Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }

    public string FullName { get; set; } = default!;

    /// <summary>
    /// Optional profile image stored in <see cref="Files"/> (FK).
    /// </summary>
    public Guid? ProfileFileId { get; set; }

    public FileEntity? ProfileFile { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }
}