namespace Application.DTOs.Emails;

public enum BroadcastTarget
{
    AllTenants,       // All active confirmed users across every tenant (SystemAdmin only)
    SpecificTenants,  // All active confirmed users in the selected tenants (SystemAdmin only)
    CurrentTenant,    // All active confirmed users in the current / selected tenant
    SpecificUsers,    // Explicit list of user IDs
}

public class SendEmailBroadcastRequest
{
    public string Subject { get; set; } = default!;
    public string HtmlBody { get; set; } = default!;
    public BroadcastTarget TargetType { get; set; } = BroadcastTarget.CurrentTenant;

    // SpecificTenants: one or more tenant GUIDs (SystemAdmin only)
    public List<Guid>? TenantIds { get; set; }

    // SpecificUsers: explicit recipient user GUIDs
    public List<Guid>? UserIds { get; set; }
}
