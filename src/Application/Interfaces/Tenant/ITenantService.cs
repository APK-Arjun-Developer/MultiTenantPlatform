using Application.DTOs.Common;
using Application.DTOs.Tenant;
using Domain.Enums;

namespace Application.Interfaces.Tenant;

public interface ITenantService
{
    Task<PagedResponse<TenantResponse>> GetTenantsAsync(int page, int pageSize, string? search = null, string? sortBy = null, string? sortOrder = null, bool? isActive = null, CreatedVia? createdVia = null);

    Task<TenantResponse> GetByIdAsync(Guid id);

    Task<TenantResponse> GetCurrentAsync();

    Task<OnboardTenantResponse> OnboardTenantAsync(OnboardTenantRequest request);

    Task<TenantResponse> UpdateAsync(UpdateTenantRequest request);

    Task<TenantResponse> UpdateCurrentTenantAddressAsync(UpdateCurrentTenantAddressRequest request);

    Task DeleteAsync(DeleteTenantRequest request);
}
