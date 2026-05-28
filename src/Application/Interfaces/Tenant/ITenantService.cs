using Application.DTOs.Tenant;

namespace Application.Interfaces.Tenant;

public interface ITenantService
{
    Task<TenantResponse> CreateAsync(
        CreateTenantRequest request);

    Task<List<TenantResponse>> GetAllAsync();

    Task CreateTenantAdminAsync(
        Guid tenantId,
        CreateTenantAdminRequest request);
}