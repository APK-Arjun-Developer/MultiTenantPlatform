using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity.Entities;

public class ApplicationRole : IdentityRole<Guid>
{
    public Guid TenantId { get; set; }

    public string? Description { get; set; }

    public DateTime? DeletedAt { get; set; }
}