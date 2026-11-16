using System.Net;

namespace ELearningPlatform.API.Tests.Infrastructure;

public static class ResponseAssert
{
    /// <summary>Asserts the status code and shows the response body when it doesn't match.</summary>
    public static async Task StatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected {(int)expected} {expected} but got {(int)response.StatusCode} {response.StatusCode} for {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}. Body: {body}");
        }
    }
}
