using Application.DTOs.Common;
using Application.DTOs.Subscription;
using Domain.Enums;

namespace Application.DTOs.Tenant;

public class TenantResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public bool IsActive { get; set; }

    public CreatedVia CreatedVia { get; set; }

    public Guid? ProfileFileId { get; set; }

    public AddressResponse? Address { get; set; }

    public string? AdminEmail { get; set; }

    public string PlanType { get; set; } = "Free";

    public string PlanName { get; set; } = "Free";

    public PlanFeatureSummary PlanFeatures { get; set; } = default!;
}