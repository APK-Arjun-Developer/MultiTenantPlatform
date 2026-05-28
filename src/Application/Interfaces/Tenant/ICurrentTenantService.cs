namespace Application.Interfaces.Tenant;

public interface ICurrentTenantService
{
    Guid? TenantId { get; }

    Guid? UserId { get; }

    Guid? RoleId { get; }

    string? Email { get; }
}