using Domain.Contracts;

namespace Infrastructure.Persistence;

public static class SoftDeleteExtensions
{
    public static void MarkDeleted(this IAuditableEntity entity, Guid? deletedBy = null)
    {
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = deletedBy;
    }
}
