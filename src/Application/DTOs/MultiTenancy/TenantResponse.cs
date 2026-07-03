using Application.DTOs.Common;
using Domain.Enums;

namespace Application.DTOs.Tenant;

public class TenantResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string Slug { get; set; } = default!;

    public bool IsActive { get; set; }

    public CreatedVia CreatedVia { get; set; }

    public Guid? ProfileFileId { get; set; }

    public string? ProfileUrl { get; set; }

    public AddressResponse? Address { get; set; }
}