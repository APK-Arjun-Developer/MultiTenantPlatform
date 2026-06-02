namespace Api.Options;

public class SwaggerAccessOptions
{
    public const string SectionName = "Swagger";

    /// <summary>
    /// When true, Swagger UI is available in Production (behind login gate).
    /// </summary>
    public bool EnabledInProduction { get; set; }

    public string AdminUsername { get; set; } = "admin@system.com";

    public string AdminPassword { get; set; } = string.Empty;

    public int SessionHours { get; set; } = 8;
}
