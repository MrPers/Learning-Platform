using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ELearningPlatform.API.Tests.Infrastructure;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Options);
        Assert.NotNull(value);
        return value;
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>The "code" extension of a problem details response.</summary>
    public static async Task<string?> ReadProblemCodeAsync(this HttpResponseMessage response)
    {
        var json = await response.ReadJsonAsync();
        return json.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    /// <summary>Keys of the "errors" dictionary of a validation problem.</summary>
    public static async Task<string[]> ReadValidationErrorKeysAsync(this HttpResponseMessage response)
    {
        var json = await response.ReadJsonAsync();
        return json.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToArray();
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body)
        => client.PostAsJsonAsync(url, body, Options);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body)
        => client.PutAsJsonAsync(url, body, Options);

    public static Task<HttpResponseMessage> PatchJsonAsync<T>(this HttpClient client, string url, T body)
        => client.PatchAsJsonAsync(url, body, Options);

    public static Task<HttpResponseMessage> DeleteJsonAsync<T>(this HttpClient client, string url, T body)
        => client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url) { Content = JsonContent.Create(body, options: Options) });
}
