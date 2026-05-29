using Application.DTOs.Common;
using Application.DTOs.Tenant;

namespace Application.Interfaces.Tenant;

public interface ITenantService
{
    Task<PagedResponse<TenantResponse>> GetTenantsAsync(int page, int pageSize);

    Task<TenantResponse> GetCurrentAsync();

    Task<OnboardTenantResponse> OnboardTenantAsync(OnboardTenantRequest request);

    Task<TenantResponse> UpdateAsync(UpdateTenantRequest request);

    Task DeleteAsync(DeleteTenantRequest request);
}
