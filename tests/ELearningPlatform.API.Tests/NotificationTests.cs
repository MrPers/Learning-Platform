using System.Net;
using ELearningPlatform.API.Tests.Infrastructure;
using ELearningPlatform.Application.DTOs.Notification;

namespace ELearningPlatform.API.Tests;

public class NotificationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient Admin => factory.CreateHttpsClient().WithBearer(TokenFactory.Create("Admin"));

    [Fact]
    public async Task SendSms_Returns202WithJobId()
    {
        var response = await Admin.PostJsonAsync("api/Notification/send-sms", new { toPhoneNumber = "+201012345678", body = "Hello" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("job-sms", (await response.ReadJsonAsync()).GetProperty("jobId").GetString());
        factory.Notifications.Received().SendSMS(Arg.Is<SendSMSDto>(d => d.Body == "Hello"));
    }

    [Theory]
    [InlineData("12345", "Hello")]
    [InlineData("+201012345678", "")]
    public async Task SendSms_Invalid_Returns400(string toPhoneNumber, string body)
    {
        var response = await Admin.PostJsonAsync("api/Notification/send-sms", new { toPhoneNumber, body });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendEmail_WithAttachment_Returns202WithJobId()
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("someone@test.local"), "ToEmail" },
            { new StringContent("Subject"), "Subject" },
            { new StringContent("<p>Hi</p>"), "Body" },
            { TestData.File([1, 2, 3], "application/octet-stream"), "Attachments", "a.bin" },
        };

        var response = await Admin.PostAsync("api/Notification/send-email", form);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("job-email", (await response.ReadJsonAsync()).GetProperty("jobId").GetString());
        await factory.Notifications.Received().SendEmailAsync(
            Arg.Is<SendEmailDto>(d => d.ToEmail == "someone@test.local" && d.Attachments!.Count == 1), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("not-an-email", "Subject", "Body")]
    [InlineData("a@test.local", "", "Body")]
    [InlineData("a@test.local", "Subject", "")]
    public async Task SendEmail_Invalid_Returns400(string to, string subject, string body)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(to), "ToEmail" },
            { new StringContent(subject), "Subject" },
            { new StringContent(body), "Body" },
        };

        var response = await Admin.PostAsync("api/Notification/send-email", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendEmail_TooManyAttachments_Returns400()
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("a@test.local"), "ToEmail" },
            { new StringContent("S"), "Subject" },
            { new StringContent("B"), "Body" },
        };
        for (var i = 0; i < 6; i++)
            form.Add(TestData.File([1], "application/octet-stream"), "Attachments", $"f{i}.bin");

        var response = await Admin.PostAsync("api/Notification/send-email", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
