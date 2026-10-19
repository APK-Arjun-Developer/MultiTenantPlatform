using Application.DTOs.TenantConfig;

namespace Application.Interfaces.TenantConfig;

public interface ITenantConfigService
{
    Task<TenantConfigResponse> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TenantConfigResponse> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<TenantConfigResponse> UpdateByTenantIdAsync(UpdateTenantConfigRequest request, CancellationToken cancellationToken = default);

    Task<TenantConfigResponse> UpdateCurrentAsync(UpdateCurrentTenantConfigRequest request, CancellationToken cancellationToken = default);

    // Returns the config row, creating a default one if it does not exist yet.
    Task<Domain.Entities.TenantConfig> GetOrCreateAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
