using System.Net.Http.Headers;

namespace ELearningPlatform.API.Tests.Infrastructure;

public static class TestData
{
    public const string Password = "Str0ng!Pass";
    /// <summary>
    /// Accepted by the current registration regex (^\+2(10|11|12|15)[0-9]{8}$). Real Egyptian numbers are
    /// +20 1x xxxx xxxx and are rejected - see RegistrationTests.Register_RealEgyptianNumber_IsAccepted.
    /// </summary>
    public const string Phone = "+201012345678";

    public static string UniqueEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@test.local";

    public static string Unique(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>PNG header + padding.</summary>
    public static byte[] Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. new byte[64]];

    /// <summary>ISO base media header: box size, then "ftyp" at offset 4.</summary>
    public static byte[] Mp4 => [0x00, 0x00, 0x00, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'m', (byte)'p', (byte)'4', (byte)'2', .. Enumerable.Range(0, 256).Select(i => (byte)i)];

    /// <summary>A Windows executable header ("MZ").</summary>
    public static byte[] Exe => [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, .. new byte[64]];

    public static ByteArrayContent File(byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return content;
    }

    public static MultipartFormDataContent RegisterForm(string email, string role, string password = Password,
        string? confirmPassword = null, string phone = Phone, string specialization = "ComputerScience",
        (byte[] Bytes, string FileName, string ContentType)? picture = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("Test"), "FirstName" },
            { new StringContent("User"), "LastName" },
            { new StringContent(phone), "PhoneNumber" },
            { new StringContent(email), "Email" },
            { new StringContent("Female"), "Gender" },
            { new StringContent(password), "Password" },
            { new StringContent(confirmPassword ?? password), "ConfirmPassword" },
        };
        if (role == "Teacher")
            form.Add(new StringContent(specialization), "Specialization");
        else
            form.Add(new StringContent("Beginner"), "Level");

        if (picture is { } p)
            form.Add(File(p.Bytes, p.ContentType), "ProfilePictureFile", p.FileName);
        return form;
    }

    public static MultipartFormDataContent VideoForm(byte[] bytes, string fileName = "lesson.mp4", string title = "Lesson", string contentType = "video/mp4")
        => new()
        {
            { new StringContent(title), "Title" },
            { new StringContent("A lesson"), "Description" },
            { new StringContent(fileName), "OriginalName" },
            { File(bytes, contentType), "VideoFile", fileName },
        };
}
