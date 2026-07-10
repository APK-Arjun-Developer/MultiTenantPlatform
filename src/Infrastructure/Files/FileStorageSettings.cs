namespace Infrastructure.Files;

public class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    public string BasePath { get; set; } = "storage";
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
    public ImageProcessingSettings ImageProcessing { get; set; } = new();
}

public class ImageProcessingSettings
{
    public bool Enabled { get; set; } = true;
    public int MaxWidth { get; set; } = 2048;
    public int MaxHeight { get; set; } = 2048;
    public int WebpQuality { get; set; } = 85;
}
