using Domain.Enums;

namespace Domain.Entities;

public class Permission
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string Description { get; set; } = default!;

    public string Module { get; set; } = default!;

    /// <summary>Minimum system role required to use this permission.</summary>
    public SystemRole RequiredSystemRole { get; set; } = SystemRole.TenantUser;

    public DateTime CreatedAt { get; set; }
}
