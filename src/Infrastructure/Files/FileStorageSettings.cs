namespace Infrastructure.Files;

public class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    public string BasePath { get; set; } = "storage";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
