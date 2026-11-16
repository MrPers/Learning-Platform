using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

namespace ELearningPlatform.Infrastructure.Tests.Services;

public class NotificationServiceTests
{
    private readonly IBackgroundJobClient _jobs = Substitute.For<IBackgroundJobClient>();
    private readonly List<Job> _created = [];

    public NotificationServiceTests()
        => _jobs.Create(Arg.Do<Job>(_created.Add), Arg.Any<IState>()).Returns(call => $"job-{_created.Count}");

    private NotificationService CreateService() => new(_jobs);

    private static SendEmailDto Email(params Microsoft.AspNetCore.Http.IFormFile[] files) => new()
    {
        ToEmail = "to@elearning.test",
        Subject = "Subject",
        Body = "<p>Body</p>",
        Attachments = files.Length == 0 ? null : files.ToList(),
    };

    [Fact]
    public async Task SendEmailAsync_EnqueuesMailServiceJob()
    {
        var id = await CreateService().SendEmailAsync(Email());

        Assert.Equal("job-1", id);
        var job = Assert.Single(_created);
        Assert.Equal(typeof(IMailService), job.Type);
        Assert.Equal(nameof(IMailService.SendEmailAsync), job.Method.Name);
        _jobs.Received(1).Create(Arg.Any<Job>(), Arg.Any<EnqueuedState>());
    }

    [Fact]
    public async Task SendEmailAsync_PassesRecipientSubjectAndBody()
    {
        await CreateService().SendEmailAsync(Email());

        var args = Assert.Single(_created).Args;
        Assert.Equal("to@elearning.test", args[0]);
        Assert.Equal("Subject", args[1]);
        Assert.Equal("<p>Body</p>", args[2]);
        Assert.Null(args[3]);
    }

    [Fact]
    public async Task SendEmailAsync_CopiesAttachmentBytes()
    {
        byte[] content = [10, 20, 30];
        await CreateService().SendEmailAsync(Email(TestFiles.FormFile("notes.pdf", content, "application/pdf")));

        var attachments = Assert.IsAssignableFrom<IReadOnlyList<EmailAttachment>>(Assert.Single(_created).Args[3]);
        var attachment = Assert.Single(attachments);
        Assert.Equal("notes.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(content, attachment.Content);
    }

    [Fact]
    public async Task SendEmailAsync_AttachmentsSurviveDisposedRequestStream()
    {
        var stream = new MemoryStream([1, 2, 3]);
        var file = new Microsoft.AspNetCore.Http.FormFile(stream, 0, 3, "file", "a.txt")
        {
            Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
            ContentType = "text/plain",
        };

        await CreateService().SendEmailAsync(Email(file));
        stream.Dispose(); // the request ends; the job still has its own copy

        var attachment = ((IReadOnlyList<EmailAttachment>)_created[0].Args[3]!)[0];
        Assert.Equal([1, 2, 3], attachment.Content);
    }

    [Fact]
    public async Task SendEmailAsync_SkipsEmptyFiles()
    {
        await CreateService().SendEmailAsync(Email(
            TestFiles.FormFile("empty.txt", []),
            TestFiles.FormFile("full.txt", [1])));

        var attachments = (IReadOnlyList<EmailAttachment>)_created[0].Args[3]!;
        Assert.Equal("full.txt", Assert.Single(attachments).FileName);
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("folder/sub/report.pdf", "report.pdf")]
    [InlineData("report.pdf", "report.pdf")]
    public async Task SendEmailAsync_StripsDirectoriesFromFileName(string fileName, string expected)
    {
        await CreateService().SendEmailAsync(Email(TestFiles.FormFile(fileName, [1])));

        Assert.Equal(expected, ((IReadOnlyList<EmailAttachment>)_created[0].Args[3]!)[0].FileName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendEmailAsync_MissingContentType_UsesOctetStream(string contentType)
    {
        await CreateService().SendEmailAsync(Email(TestFiles.FormFile("blob.bin", [1], contentType)));

        Assert.Equal("application/octet-stream", ((IReadOnlyList<EmailAttachment>)_created[0].Args[3]!)[0].ContentType);
    }

    [Fact]
    public async Task SendEmailAsync_KeepsAllAttachmentsInOrder()
    {
        await CreateService().SendEmailAsync(Email(
            TestFiles.FormFile("1.txt", [1]),
            TestFiles.FormFile("2.txt", [2]),
            TestFiles.FormFile("3.txt", [3])));

        Assert.Equal(["1.txt", "2.txt", "3.txt"], ((IReadOnlyList<EmailAttachment>)_created[0].Args[3]!).Select(a => a.FileName));
    }

    [Fact]
    public async Task SendEmailAsync_JobArgumentsRoundTripThroughHangfireSerialization()
    {
        byte[] content = [0, 1, 2, 250, 255];
        await CreateService().SendEmailAsync(Email(TestFiles.FormFile("data.bin", content, "application/octet-stream")));

        var restored = InvocationData.SerializeJob(_created[0]).DeserializeJob();

        Assert.Equal(typeof(IMailService), restored.Type);
        Assert.Equal("to@elearning.test", restored.Args[0]);
        var attachment = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<EmailAttachment>>(restored.Args[3]));
        Assert.Equal("data.bin", attachment.FileName);
        Assert.Equal(content, attachment.Content);
    }

    [Fact]
    public void SendSMS_EnqueuesSmsServiceJob()
    {
        var id = CreateService().SendSMS(new SendSMSDto { ToPhoneNumber = "+201012345678", Body = "code 1" });

        Assert.Equal("job-1", id);
        var job = Assert.Single(_created);
        Assert.Equal(typeof(ISMSService), job.Type);
        Assert.Equal(nameof(ISMSService.SendSMSAsync), job.Method.Name);
        Assert.Equal("+201012345678", job.Args[0]);
        Assert.Equal("code 1", job.Args[1]);
    }

    [Fact]
    public void SendSMS_JobRoundTripsThroughHangfireSerialization()
    {
        CreateService().SendSMS(new SendSMSDto { ToPhoneNumber = "+201012345678", Body = "hello" });

        var restored = InvocationData.SerializeJob(_created[0]).DeserializeJob();

        Assert.Equal(typeof(ISMSService), restored.Type);
        Assert.Equal("hello", restored.Args[1]);
    }

    [Fact]
    public async Task ToAttachmentsAsync_NoFiles_ReturnsEmpty()
        => Assert.Empty(await NotificationService.ToAttachmentsAsync([], CancellationToken.None));
}
