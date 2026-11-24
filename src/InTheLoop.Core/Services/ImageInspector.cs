namespace InTheLoop.Core.Services;

/// <summary>Identifies an image by its leading bytes so the store never trusts a file name.</summary>
public static class ImageInspector
{
    public const int MaxBytes = 25 * 1024 * 1024;

    public static string? DetectExtension(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith<byte>([0x89, 0x50, 0x4E, 0x47]))
        {
            return ".png";
        }

        if (data.StartsWith<byte>([0xFF, 0xD8, 0xFF]))
        {
            return ".jpg";
        }

        if (data.StartsWith("GIF8"u8))
        {
            return ".gif";
        }

        if (data.StartsWith("BM"u8))
        {
            return ".bmp";
        }

        if (data.Length >= 12 && data.StartsWith("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return ".webp";
        }

        return null;
    }
}
