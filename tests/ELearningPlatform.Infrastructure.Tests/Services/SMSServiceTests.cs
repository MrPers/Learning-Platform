using System.Net;
using Twilio.Clients;
using Twilio.Exceptions;
using TwilioRequest = Twilio.Http.Request;
using TwilioResponse = Twilio.Http.Response;

namespace ELearningPlatform.Infrastructure.Tests.Services;

public class SMSServiceTests
{
    private readonly ITwilioRestClient _client = Substitute.For<ITwilioRestClient>();
    private TwilioRequest? _lastRequest;

    public SMSServiceTests() => _client.AccountSid.Returns("ACtest");

    private SMSService CreateService() => new(_client, Options.Create(TestSettings.Twilio()), NullLogger<SMSService>.Instance);

    private void GivenResponse(HttpStatusCode status, string json)
        => _client.RequestAsync(Arg.Any<TwilioRequest>()).Returns(call =>
        {
            _lastRequest = call.Arg<TwilioRequest>();
            return Task.FromResult(new TwilioResponse(status, json, null));
        });

    private static string MessageJson(int? errorCode = null, string? errorMessage = null)
        => $$"""
           {
             "sid": "SM123",
             "account_sid": "ACtest",
             "status": "{{(errorCode == null ? "queued" : "failed")}}",
             "to": "+201012345678",
             "from": "+15550001111",
             "body": "hi",
             "error_code": {{(errorCode?.ToString() ?? "null")}},
             "error_message": {{(errorMessage == null ? "null" : $"\"{errorMessage}\"")}}
           }
           """;

    private string? Param(string name) => _lastRequest!.PostParams.FirstOrDefault(p => p.Key == name).Value;

    [Fact]
    public async Task SendSMSAsync_Success_DoesNotThrow()
    {
        GivenResponse(HttpStatusCode.Created, MessageJson());

        await CreateService().SendSMSAsync("+201012345678", "hi");

        await _client.Received(1).RequestAsync(Arg.Any<TwilioRequest>());
    }

    [Fact]
    public async Task SendSMSAsync_PostsToFromAndBody()
    {
        GivenResponse(HttpStatusCode.Created, MessageJson());

        await CreateService().SendSMSAsync("+201012345678", "Your code is 123456");

        Assert.Equal("+201012345678", Param("To"));
        Assert.Equal("+15550001111", Param("From"));
        Assert.Equal("Your code is 123456", Param("Body"));
    }

    [Fact]
    public async Task SendSMSAsync_UsesInjectedClientAccount()
    {
        GivenResponse(HttpStatusCode.Created, MessageJson());

        await CreateService().SendSMSAsync("+201012345678", "hi");

        Assert.Contains("ACtest", _lastRequest!.ConstructUrl().ToString());
    }

    [Theory]
    [InlineData(30003, "Unreachable destination handset")]
    [InlineData(21610, "Attempt to send to unsubscribed recipient")]
    public async Task SendSMSAsync_ErrorCodeInResponse_Throws(int code, string message)
    {
        GivenResponse(HttpStatusCode.Created, MessageJson(code, message));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().SendSMSAsync("+201012345678", "hi"));

        Assert.Contains(code.ToString(), ex.Message);
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public async Task SendSMSAsync_TwilioApiError_PropagatesSoJobIsRetried()
    {
        // The real TwilioRestClient turns non-2xx responses into ApiException; it must not be swallowed
        _client.RequestAsync(Arg.Any<TwilioRequest>()).Returns(Task.FromException<TwilioResponse>(new ApiException("Authenticate")));

        await Assert.ThrowsAsync<ApiException>(() => CreateService().SendSMSAsync("+201012345678", "hi"));
    }

    [Fact]
    public async Task SendSMSAsync_ClientThrows_Propagates()
    {
        _client.RequestAsync(Arg.Any<TwilioRequest>()).Returns(Task.FromException<TwilioResponse>(new HttpRequestException("offline")));

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateService().SendSMSAsync("+201012345678", "hi"));
    }
}
