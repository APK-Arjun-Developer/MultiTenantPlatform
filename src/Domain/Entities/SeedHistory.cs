namespace Domain.Entities;

public class SeedHistory
{
    public string SeedId { get; set; } = default!;

    public DateTime AppliedAt { get; set; }

    public string? Description { get; set; }
}
