namespace Application.DTOs.TenantConfig;

// TenantAdmin: updates config for their own tenant (no TenantId parameter).
// Maintenance mode is SystemAdmin-only and is intentionally excluded here.
public class UpdateCurrentTenantConfigRequest
{
    public bool LoginEnabled { get; set; }
    public bool WelcomeEmailEnabled { get; set; }
}
