using Domain.Contracts;
using Domain.Enums;

namespace Domain.Entities;

// Tenant is the root aggregate — it is NOT a tenant-scoped entity and must not carry TenantId.
// It implements IAuditableEntity for soft-delete and audit fields only.
public class Tenant : IAuditableEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public bool IsActive { get; set; } = true;

    public CreatedVia CreatedVia { get; set; }

    public PlanType PlanType { get; set; } = PlanType.Free;

    public Guid? ProfileFileId { get; set; }

    public FileEntity? ProfileFile { get; set; }

    public Address? Address { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }
}