using Application.DTOs.Common;
using Application.DTOs.Tenant;

namespace Application.Interfaces.Tenant;

public interface ITenantService
{
    Task<PagedResponse<TenantResponse>> GetTenantsAsync(int page, int pageSize, string? search = null, string? sortBy = null, string? sortOrder = null);

    Task<TenantResponse> GetByIdAsync(Guid id);

    Task<TenantResponse> GetCurrentAsync();

    Task<OnboardTenantResponse> OnboardTenantAsync(OnboardTenantRequest request);

    Task<TenantResponse> UpdateAsync(UpdateTenantRequest request);

    Task DeleteAsync(DeleteTenantRequest request);
}
