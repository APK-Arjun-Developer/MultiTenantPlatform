namespace Infrastructure.Persistence.Seed;

public interface IDataSeed
{
    string SeedId { get; }

    string Description { get; }

    Task ApplyAsync(CancellationToken cancellationToken = default);
}
