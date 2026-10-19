namespace Application.DTOs.TenantConfig;

// SystemAdmin: can update any tenant's config by providing TenantId.
public class UpdateTenantConfigRequest
{
    public Guid TenantId { get; set; }
    public bool LoginEnabled { get; set; }
    public bool WelcomeEmailEnabled { get; set; }
    public bool MaintenanceMode { get; set; }
    public string? MaintenanceMessage { get; set; }
}
