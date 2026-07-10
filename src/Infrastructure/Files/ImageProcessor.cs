using SkiaSharp;

namespace Infrastructure.Files;

internal static class ImageProcessor
{
    private static readonly HashSet<string> ProcessableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/bmp",
    };

    public static bool IsProcessable(string contentType) => ProcessableTypes.Contains(contentType);

    public static Task<MemoryStream> ProcessAsync(Stream input, ImageProcessingSettings settings)
    {
        using var inputData = SKData.Create(input);
        using var original = SKBitmap.Decode(inputData)
            ?? throw new InvalidOperationException("Could not decode image.");

        SKBitmap bitmap = original;
        SKBitmap? resized = null;

        try
        {
            if (original.Width > settings.MaxWidth || original.Height > settings.MaxHeight)
            {
                float ratio = Math.Min(
                    (float)settings.MaxWidth / original.Width,
                    (float)settings.MaxHeight / original.Height);

                resized = original.Resize(
                    new SKImageInfo((int)(original.Width * ratio), (int)(original.Height * ratio)),
                    SKSamplingOptions.Default);

                bitmap = resized ?? bitmap;
            }

            using var skImage = SKImage.FromBitmap(bitmap);
            using var encoded = skImage.Encode(SKEncodedImageFormat.Webp, settings.WebpQuality);

            var output = new MemoryStream();
            encoded.SaveTo(output);
            output.Position = 0;
            return Task.FromResult(output);
        }
        finally
        {
            resized?.Dispose();
        }
    }
}
