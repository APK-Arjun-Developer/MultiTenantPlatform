namespace Application.DTOs.TenantConfig;

public class TenantConfigResponse
{
    public Guid TenantId { get; set; }
    public bool LoginEnabled { get; set; }
    public bool WelcomeEmailEnabled { get; set; }
    public bool MaintenanceMode { get; set; }
    public string? MaintenanceMessage { get; set; }
}
