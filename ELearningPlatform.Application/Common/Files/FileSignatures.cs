namespace ELearningPlatform.Application.Common.Files;

/// <summary>Magic-byte signatures of the file types the platform accepts.</summary>
public static class FileSignatures
{
    // Each pattern is (offset, bytes); a file matches when every pattern of one alternative matches.
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Ftyp = "ftyp"u8.ToArray();
    private static readonly byte[] Ebml = [0x1A, 0x45, 0xDF, 0xA3];

    private static readonly Dictionary<string, (int Offset, byte[] Bytes)[][]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = [[(0, [0xFF, 0xD8, 0xFF])]],
        [".jpeg"] = [[(0, [0xFF, 0xD8, 0xFF])]],
        [".png"] = [[(0, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])]],
        [".gif"] = [[(0, "GIF87a"u8.ToArray())], [(0, "GIF89a"u8.ToArray())]],
        [".bmp"] = [[(0, "BM"u8.ToArray())]],
        [".webp"] = [[(0, Riff), (8, "WEBP"u8.ToArray())]],
        [".mp4"] = [[(4, Ftyp)]],
        [".m4v"] = [[(4, Ftyp)]],
        [".mov"] = [[(4, Ftyp)], [(4, "moov"u8.ToArray())]],
        [".avi"] = [[(0, Riff), (8, "AVI "u8.ToArray())]],
        [".mkv"] = [[(0, Ebml)]],
        [".webm"] = [[(0, Ebml)]],
        [".wmv"] = [[(0, [0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11])]],
        [".pdf"] = [[(0, "%PDF"u8.ToArray())]],
        [".zip"] = [[(0, [0x50, 0x4B, 0x03, 0x04])]],
        [".docx"] = [[(0, [0x50, 0x4B, 0x03, 0x04])]],
        [".xlsx"] = [[(0, [0x50, 0x4B, 0x03, 0x04])]],
        [".pptx"] = [[(0, [0x50, 0x4B, 0x03, 0x04])]],
    };

    private static readonly int HeaderLength = Signatures.Values
        .SelectMany(alternatives => alternatives)
        .SelectMany(patterns => patterns)
        .Max(p => p.Offset + p.Bytes.Length);

    public static bool IsKnown(string extension) => Signatures.ContainsKey(extension);

    /// <summary>
    /// True when <paramref name="content"/> starts with a signature registered for <paramref name="extension"/>.
    /// Unknown extensions never match: a type we can't verify is not accepted.
    /// </summary>
    public static bool Matches(string extension, Stream content)
    {
        if (!Signatures.TryGetValue(extension, out var alternatives))
            return false;

        var header = new byte[HeaderLength];
        var read = content.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        return alternatives.Any(patterns => patterns.All(p => StartsWithAt(header, read, p.Offset, p.Bytes)));
    }

    private static bool StartsWithAt(byte[] header, int length, int offset, byte[] expected)
        => length >= offset + expected.Length && header.AsSpan(offset, expected.Length).SequenceEqual(expected);
}
