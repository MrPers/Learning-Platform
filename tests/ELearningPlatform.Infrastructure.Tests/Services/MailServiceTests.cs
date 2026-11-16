using System.Net;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MailService = ELearningPlatform.Infrastructure.Services.MailService;

namespace ELearningPlatform.Infrastructure.Tests.Services;

public class MailServiceTests
{
    private sealed class TestableMailService(ISmtpClient client)
        : MailService(Options.Create(TestSettings.Mail()), NullLogger<MailService>.Instance)
    {
        protected override ISmtpClient CreateSmtpClient() => client;
    }

    private static MailService CreateService() => new(Options.Create(TestSettings.Mail()), NullLogger<MailService>.Instance);

    [Fact]
    public void CreateEmailMessage_SetsAddressesAndSubject()
    {
        var message = CreateService().CreateEmailMessage("to@elearning.test", "Hello", "<p>Hi</p>", null);

        Assert.Equal("noreply@elearning.test", message.Sender!.Address);
        var from = Assert.Single(message.From.Mailboxes);
        Assert.Equal("noreply@elearning.test", from.Address);
        Assert.Equal("E-Learning", from.Name);
        Assert.Equal("to@elearning.test", Assert.Single(message.To.Mailboxes).Address);
        Assert.Equal("Hello", message.Subject);
    }

    [Fact]
    public void CreateEmailMessage_UsesHtmlBody()
    {
        var message = CreateService().CreateEmailMessage("to@elearning.test", "s", "<b>bold</b>", null);

        Assert.Equal("<b>bold</b>", message.HtmlBody);
        Assert.Empty(message.Attachments);
    }

    [Fact]
    public void CreateEmailMessage_EmptyAttachmentList_HasNoAttachments()
    {
        var message = CreateService().CreateEmailMessage("to@elearning.test", "s", "b", []);
        Assert.Empty(message.Attachments);
    }

    [Fact]
    public void CreateEmailMessage_AttachmentUsesFileNameContentTypeAndBytes()
    {
        byte[] content = [1, 2, 3, 4, 5];
        var message = CreateService().CreateEmailMessage("to@elearning.test", "s", "b",
            [new EmailAttachment("report.pdf", "application/pdf", content)]);

        var part = Assert.IsType<MimePart>(Assert.Single(message.Attachments));
        Assert.Equal("report.pdf", part.FileName);
        Assert.Equal("application/pdf", part.ContentType.MimeType);
        using var ms = new MemoryStream();
        part.Content!.DecodeTo(ms);
        Assert.Equal(content, ms.ToArray());
    }

    [Fact]
    public void CreateEmailMessage_KeepsEveryAttachmentInOrder()
    {
        var message = CreateService().CreateEmailMessage("to@elearning.test", "s", "b",
        [
            new EmailAttachment("a.txt", "text/plain", [1]),
            new EmailAttachment("b.png", "image/png", [2]),
            new EmailAttachment("c.zip", "application/zip", [3]),
        ]);

        Assert.Equal(["a.txt", "b.png", "c.zip"], message.Attachments.Select(a => a.ContentDisposition?.FileName ?? ((MimePart)a).FileName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a@b@c")]
    public void CreateEmailMessage_InvalidRecipient_Throws(string to)
        => Assert.ThrowsAny<ParseException>(() => CreateService().CreateEmailMessage(to, "s", "b", null));

    [Fact]
    public async Task SendEmailAsync_ConnectsAuthenticatesSendsAndDisconnects()
    {
        var smtp = Substitute.For<ISmtpClient>();
        smtp.IsConnected.Returns(true);

        await new TestableMailService(smtp).SendEmailAsync("to@elearning.test", "Subject", "Body");

        var calls = smtp.ReceivedCalls().Select(c => c.GetMethodInfo().Name)
            .Where(n => n is "ConnectAsync" or "AuthenticateAsync" or "SendAsync" or "DisconnectAsync").ToList();
        Assert.Equal(["ConnectAsync", "AuthenticateAsync", "SendAsync", "DisconnectAsync"], calls);
    }

    [Fact]
    public async Task SendEmailAsync_ConnectsWithConfiguredHostPortAndStartTls()
    {
        var smtp = Substitute.For<ISmtpClient>();

        await new TestableMailService(smtp).SendEmailAsync("to@elearning.test", "Subject", "Body");

        await smtp.Received(1).ConnectAsync("smtp.test", 587, SecureSocketOptions.StartTls, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendEmailAsync_AuthenticatesWithConfiguredCredentials()
    {
        var smtp = Substitute.For<ISmtpClient>();

        await new TestableMailService(smtp).SendEmailAsync("to@elearning.test", "Subject", "Body");

        var auth = smtp.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "AuthenticateAsync").GetArguments();
        var flattened = auth.SelectMany(a => a is NetworkCredential nc ? new object?[] { nc.UserName, nc.Password } : [a]).ToList();
        Assert.Contains("noreply@elearning.test", flattened);
        Assert.Contains("smtp-password", flattened);
    }

    [Fact]
    public async Task SendEmailAsync_SendsMessageToRecipient()
    {
        var smtp = Substitute.For<ISmtpClient>();

        await new TestableMailService(smtp).SendEmailAsync("to@elearning.test", "Subject", "Body",
            [new EmailAttachment("a.txt", "text/plain", [65])]);

        var sent = smtp.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "SendAsync").GetArguments().OfType<MimeMessage>().Single();
        Assert.Equal("to@elearning.test", sent.To.Mailboxes.Single().Address);
        Assert.Equal("Subject", sent.Subject);
        Assert.Single(sent.Attachments);
    }

    [Fact]
    public async Task SendEmailAsync_NotConnected_DoesNotDisconnect()
    {
        var smtp = Substitute.For<ISmtpClient>();
        smtp.IsConnected.Returns(false);

        await new TestableMailService(smtp).SendEmailAsync("to@elearning.test", "Subject", "Body");

        await smtp.DidNotReceive().DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendEmailAsync_AuthenticationFails_RethrowsAndDisconnects()
    {
        var smtp = Substitute.For<ISmtpClient>();
        smtp.IsConnected.Returns(true);
        smtp.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new AuthenticationException("bad credentials")));

        await Assert.ThrowsAsync<AuthenticationException>(() => new TestableMailService(smtp).SendEmailAsync("to@elearning.test", "s", "b"));

        await smtp.Received(1).DisconnectAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendEmailAsync_ConnectFails_RethrowsWithoutDisconnect()
    {
        var smtp = Substitute.For<ISmtpClient>();
        smtp.IsConnected.Returns(false);
        smtp.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("network down")));

        await Assert.ThrowsAsync<IOException>(() => new TestableMailService(smtp).SendEmailAsync("to@elearning.test", "s", "b"));

        await smtp.DidNotReceive().DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendEmailAsync_DisposesClient()
    {
        var smtp = Substitute.For<ISmtpClient>();

        await new TestableMailService(smtp).SendEmailAsync("to@elearning.test", "s", "b");

        smtp.Received(1).Dispose();
    }

    [Fact]
    public async Task SendEmailAsync_PassesCancellationTokenToSmtp()
    {
        var smtp = Substitute.For<ISmtpClient>();
        using var cts = new CancellationTokenSource();

        await new TestableMailService(smtp).SendEmailAsync("to@elearning.test", "s", "b", null, cts.Token);

        await smtp.Received(1).ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), cts.Token);
    }
}
