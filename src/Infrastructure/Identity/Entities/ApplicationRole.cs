using Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity.Entities;

public class ApplicationRole : IdentityRole<Guid>
{
    public Guid TenantId { get; set; }

    public RoleScope Scope { get; set; } = RoleScope.Tenant;

    public string? Description { get; set; }

    public DateTime? DeletedAt { get; set; }
}