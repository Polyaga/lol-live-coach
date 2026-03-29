using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LolLiveCoach.Desktop.Models;

namespace LolLiveCoach.Desktop.Services;

public sealed class PlatformAccountClient : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private Uri _platformBaseUri = new($"{OverlaySettings.DefaultPlatformBaseUrl}/", UriKind.Absolute);

    public void SetBaseAddress(string platformBaseUrl)
    {
        var normalized = platformBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? platformBaseUrl
            : $"{platformBaseUrl}/";

        _platformBaseUri = new Uri(normalized, UriKind.Absolute);
    }

    public async Task<PlatformLoginResult> LoginAsync(
        string email,
        string password,
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            BuildUri("api/platform/session"),
            new
            {
                email,
                password,
                deviceName
            },
            cancellationToken);

        var payloadText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorPayload = JsonSerializer.Deserialize<PlatformErrorResponse>(payloadText, SerializerOptions);
            throw new InvalidOperationException(errorPayload?.Error ?? "La connexion au compte a echoue.");
        }

        var payload = JsonSerializer.Deserialize<PlatformLoginResult>(payloadText, SerializerOptions);

        if (payload is null || string.IsNullOrWhiteSpace(payload.Token))
        {
            throw new InvalidOperationException("La plateforme n'a pas retourne de jeton desktop exploitable.");
        }

        return payload;
    }

    public async Task LogoutAsync(string? accessToken, CancellationToken cancellationToken = default)
    {
        var normalized = accessToken?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Delete, BuildUri("api/platform/session"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", normalized);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private Uri BuildUri(string relativePath) => new(_platformBaseUri, relativePath);

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private sealed class PlatformErrorResponse
    {
        public string? Error { get; set; }
    }
}
