namespace Domain.Contracts;

public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }

    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }

    DateTime? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
}