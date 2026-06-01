using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }

    public string FullName { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }
}