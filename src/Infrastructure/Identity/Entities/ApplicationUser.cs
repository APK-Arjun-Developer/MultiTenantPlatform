using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }

    public SystemRole SystemRole { get; set; }

    public string FullName { get; set; } = default!;

    public Guid? ProfileFileId { get; set; }

    public FileEntity? ProfileFile { get; set; }

    public Address? Address { get; set; }

    public bool IsActive { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime? PasswordSetAt { get; set; }

    public CreatedVia CreatedVia { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }
}