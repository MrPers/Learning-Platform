namespace ELearningPlatform.Application.Tests.Common;

public class FileValidationTests
{
    private static byte[] Bytes(params object[] parts)
        => parts.SelectMany(p => p switch
        {
            string s => System.Text.Encoding.ASCII.GetBytes(s),
            byte[] b => b,
            int n => new byte[n],
            _ => throw new ArgumentException(),
        }).Concat(new byte[16]).ToArray();

    public static TheoryData<string, byte[]> ValidFiles => new()
    {
        { ".jpg", Bytes(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }) },
        { ".jpeg", Bytes(new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }) },
        { ".png", Bytes(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) },
        { ".gif", Bytes("GIF89a") },
        { ".gif", Bytes("GIF87a") },
        { ".bmp", Bytes("BM") },
        { ".webp", Bytes("RIFF", 4, "WEBP") },
        { ".mp4", Bytes(4, "ftypisom") },
        { ".m4v", Bytes(4, "ftypM4V ") },
        { ".mov", Bytes(4, "ftypqt  ") },
        { ".mov", Bytes(4, "moov") },
        { ".avi", Bytes("RIFF", 4, "AVI ") },
        { ".mkv", Bytes(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }) },
        { ".webm", Bytes(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }) },
        { ".wmv", Bytes(new byte[] { 0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11 }) },
        { ".pdf", Bytes("%PDF-1.7") },
        { ".zip", Bytes(new byte[] { 0x50, 0x4B, 0x03, 0x04 }) },
        { ".docx", Bytes(new byte[] { 0x50, 0x4B, 0x03, 0x04 }) },
        { ".xlsx", Bytes(new byte[] { 0x50, 0x4B, 0x03, 0x04 }) },
        { ".pptx", Bytes(new byte[] { 0x50, 0x4B, 0x03, 0x04 }) },
        { ".PNG", Bytes(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) },
    };

    [Theory]
    [MemberData(nameof(ValidFiles))]
    public void Real_content_matches_its_extension(string extension, byte[] content)
    {
        Assert.True(FileSignatures.Matches(extension, new MemoryStream(content)));
    }

    public static TheoryData<string, byte[]> Disguised => new()
    {
        { ".png", FormFiles.Exe },
        { ".jpg", FormFiles.Png },
        { ".mp4", FormFiles.Jpeg },
        { ".webp", Bytes("RIFF", 4, "AVI ") },
        { ".avi", Bytes("RIFF", 4, "WEBP") },
        { ".pdf", Bytes("<html>") },
        { ".gif", Bytes("GIF90a") },
        { ".mp4", Bytes("ftyp") },
    };

    [Theory]
    [MemberData(nameof(Disguised))]
    public void Disguised_content_is_rejected(string extension, byte[] content)
    {
        Assert.False(FileSignatures.Matches(extension, new MemoryStream(content)));
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".txt")]
    [InlineData(".svg")]
    [InlineData("")]
    public void Unknown_extensions_never_match(string extension)
    {
        Assert.False(FileSignatures.IsKnown(extension));
        Assert.False(FileSignatures.Matches(extension, new MemoryStream(FormFiles.Png)));
    }

    [Fact]
    public void Content_shorter_than_the_signature_does_not_match()
    {
        Assert.False(FileSignatures.Matches(".png", new MemoryStream([0x89, 0x50])));
        Assert.False(FileSignatures.Matches(".png", new MemoryStream([])));
    }

    [Theory]
    [InlineData("photo.png", true)]
    [InlineData("photo.PNG", true)]
    [InlineData("photo.jpg", true)]
    [InlineData("photo.gif", false)]
    [InlineData("photo", false)]
    [InlineData("photo.png.exe", false)]
    public void Extension_check(string fileName, bool expected)
    {
        Assert.Equal(expected, FileValidationHelper.CheckExtension(FormFiles.Create(fileName, FormFiles.Png), [".png", ".jpg"]));
    }

    [Fact]
    public void Extension_check_of_null_file_is_false()
    {
        Assert.False(FileValidationHelper.CheckExtension(null, [".png"]));
        Assert.False(FileValidationHelper.CheckSize(null, 1));
        Assert.False(FileValidationHelper.CheckSignature(null));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1024 * 1024, true)]
    [InlineData(1024 * 1024 + 1, false)]
    [InlineData(0, false)]
    public void Size_check(int size, bool expected)
    {
        var file = size == 0 ? FormFiles.Create("a.png", []) : FormFiles.OfSize("a.png", [], size);

        Assert.Equal(expected, FileValidationHelper.CheckSize(file, 1));
    }

    [Fact]
    public void Size_check_does_not_overflow_for_large_limits()
    {
        Assert.True(FileValidationHelper.CheckSize(FormFiles.OfSize("a.mp4", [], 10), 5000));
    }

    [Theory]
    [InlineData("a.png", true)]
    [InlineData("a.jpg", false)]
    [InlineData("a.exe", false)]
    public void Signature_check_on_form_file(string name, bool expected)
    {
        Assert.Equal(expected, FileValidationHelper.CheckSignature(FormFiles.Create(name, FormFiles.Png)));
    }
}
