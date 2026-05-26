using Domain.Contracts;

namespace Domain.Common;

public abstract class BaseEntity : ITenantEntity, IAuditableEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}