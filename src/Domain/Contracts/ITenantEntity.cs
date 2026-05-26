namespace Domain.Contracts;

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}