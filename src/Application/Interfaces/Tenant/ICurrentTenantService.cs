namespace Application.Interfaces.Tenant;

public interface ICurrentTenantService
{
    /// <summary>True when the authenticated user is a SystemAdmin (JWT tenant_id = Guid.Empty).</summary>
    bool IsSystemAdmin { get; }

    /// <summary>
    /// Effective tenant context for this request.
    /// For TenantAdmin / TenantUser: from JWT tenant_id claim.
    /// For SystemAdmin: from X-Tenant-Id request header (null when header is absent).
    /// </summary>
    Guid? TenantId { get; }

    Guid? UserId { get; }

    /// <summary>First role ID from the token — kept for single-role compatibility.</summary>
    Guid? RoleId { get; }

    /// <summary>All role IDs from the token — use this for multi-role-aware logic.</summary>
    IReadOnlyList<Guid> RoleIds { get; }

    string? Email { get; }
}