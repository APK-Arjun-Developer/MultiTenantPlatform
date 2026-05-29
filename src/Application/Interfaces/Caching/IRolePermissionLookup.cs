namespace Application.Interfaces.Caching;

public interface IRolePermissionLookup
{
    Task<RolePermissionSnapshot> GetAsync(Guid roleId);

    void Invalidate(Guid roleId);
}

public sealed class RolePermissionSnapshot
{
    public required IReadOnlyList<Guid> PermissionIds { get; init; }

    public required IReadOnlyList<string> PermissionNames { get; init; }
}
