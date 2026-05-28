using Application.DTOs.Tenant;

namespace Application.Interfaces.Tenant;

public interface ITenantService
{
    Task<List<TenantResponse>> GetAllAsync();

    Task<TenantResponse> GetCurrentAsync();

    Task<OnboardTenantResponse> OnboardTenantAsync(OnboardTenantRequest request);

    Task<TenantResponse> UpdateAsync(UpdateTenantRequest request);

    Task DeleteAsync(DeleteTenantRequest request);
}
