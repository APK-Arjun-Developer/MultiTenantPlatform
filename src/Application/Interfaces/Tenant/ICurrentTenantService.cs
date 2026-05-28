namespace Application.Interfaces.Tenant;

public interface ICurrentTenantService
{
    Guid? TenantId { get; }

    Guid? UserId { get; }

    string? Email { get; }
}