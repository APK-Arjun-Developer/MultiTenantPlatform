namespace Application.DTOs.Auth;

/// <summary>
/// Login credentials. Omit <see cref="TenantSlug"/> (or leave null/empty) for platform
/// system-admin users (tenant_id = 00000000-0000-0000-0000-000000000000).
/// Provide <see cref="TenantSlug"/> for tenant-scoped users; the tenant must exist, be active, and not be soft-deleted.
/// </summary>
public class LoginRequest
{
    public string Email { get; set; } = default!;

    public string Password { get; set; } = default!;

    /// <summary>
    /// Tenant URL slug (e.g. "acme-corp"). Optional for platform SuperAdmin login only.
    /// </summary>
    public string? TenantSlug { get; set; }
}