using Application.DTOs.Common;

namespace Application.DTOs.Users;

public class UserResponse
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = default!;

    public string Email { get; set; } = default!;

    public Guid TenantId { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = [];

    public Guid? ProfileFileId { get; set; }

    public string? ProfileUrl { get; set; }

    public AddressResponse? Address { get; set; }

    public UserTenantDetails? Tenant { get; set; }
}

public class UserTenantDetails
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string Slug { get; set; } = default!;

    public bool IsActive { get; set; }

    public Guid? ProfileFileId { get; set; }

    public string? ProfileUrl { get; set; }

    public AddressResponse? Address { get; set; }
}
