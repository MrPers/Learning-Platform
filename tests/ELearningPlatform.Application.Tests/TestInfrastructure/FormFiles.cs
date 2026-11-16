using Microsoft.AspNetCore.Http;

namespace ELearningPlatform.Application.Tests.TestInfrastructure;

public static class FormFiles
{
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];
    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10];
    public static readonly byte[] Mp4 = [0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'m', (byte)'p', (byte)'4', (byte)'2'];
    public static readonly byte[] Exe = [(byte)'M', (byte)'Z', 0x90, 0, 3, 0, 0, 0, 4, 0, 0, 0];

    public static IFormFile Create(string fileName, byte[] content, string contentType = "application/octet-stream")
        => new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };

    public static IFormFile OfSize(string fileName, byte[] header, long size)
    {
        var content = new byte[size];
        header.CopyTo(content, 0);
        return Create(fileName, content);
    }
}
