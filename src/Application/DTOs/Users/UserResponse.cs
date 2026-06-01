namespace Application.DTOs.Users;

public class UserResponse
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = default!;

    public string Email { get; set; } = default!;

    public Guid TenantId { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = [];

    /// <summary>FK to Files table when a profile image is set.</summary>
    public Guid? ProfileFileId { get; set; }

    /// <summary>API path to download the profile file (requires auth).</summary>
    public string? ProfileUrl { get; set; }

    public UserTenantDetails? Tenant { get; set; }
}

public class UserTenantDetails
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string Slug { get; set; } = default!;

    public bool IsActive { get; set; }
}
