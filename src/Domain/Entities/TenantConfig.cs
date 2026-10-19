namespace Domain.Entities;

// Tenant-level configuration knobs. One row per Tenant (1:1), lazily created on first access.
// Not tenant-scoped (no ITenantEntity), not soft-deletable — config persists with the tenant.
public class TenantConfig
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public bool LoginEnabled { get; set; } = true;

    public bool WelcomeEmailEnabled { get; set; } = true;

    public bool MaintenanceMode { get; set; } = false;

    public string? MaintenanceMessage { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
