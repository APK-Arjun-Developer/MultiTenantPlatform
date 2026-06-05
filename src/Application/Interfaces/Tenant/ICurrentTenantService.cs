namespace Application.Interfaces.Tenant;

public interface ICurrentTenantService
{
    Guid? TenantId { get; }

    Guid? UserId { get; }

    /// <summary>First role ID from the token — kept for single-role compatibility.</summary>
    Guid? RoleId { get; }

    /// <summary>All role IDs from the token — use this for multi-role-aware logic.</summary>
    IReadOnlyList<Guid> RoleIds { get; }

    string? Email { get; }
}