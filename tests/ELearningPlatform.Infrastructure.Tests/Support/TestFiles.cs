using Microsoft.AspNetCore.Http;

namespace ELearningPlatform.Infrastructure.Tests.Support;

public static class TestFiles
{
    public static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
    public static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    public static readonly byte[] Mp4Header = [0x00, 0x00, 0x00, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'m', (byte)'p', (byte)'4', (byte)'2'];
    public static readonly byte[] WebmHeader = [0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x00];

    /// <summary>Header followed by filler so the file has a realistic size.</summary>
    public static byte[] WithHeader(byte[] header, int totalLength = 64)
    {
        var bytes = new byte[Math.Max(totalLength, header.Length)];
        header.CopyTo(bytes, 0);
        for (var i = header.Length; i < bytes.Length; i++)
            bytes[i] = (byte)(i % 251);
        return bytes;
    }

    public static IFormFile FormFile(string fileName, byte[] content, string contentType = "application/octet-stream", string name = "file")
        => new FormFile(new MemoryStream(content), 0, content.Length, name, fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };

    public static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "elp-infra-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
