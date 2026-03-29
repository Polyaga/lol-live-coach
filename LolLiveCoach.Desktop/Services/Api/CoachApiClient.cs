using System.Net.Http;
using System.Net.Http.Json;
using LolLiveCoach.Desktop.Models;

namespace LolLiveCoach.Desktop.Services;

public sealed class CoachApiClient : IDisposable
{
    private const string AccessKeyHeaderName = "X-Lol-Access-Key";
    private const string PlatformBaseUrlHeaderName = "X-Lol-Platform-Url";
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(6)
    };
    private Uri _apiBaseUri = new($"{OverlaySettings.DefaultApiBaseUrl}/", UriKind.Absolute);

    public void SetBaseAddress(string apiBaseUrl)
    {
        var normalized = apiBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? apiBaseUrl
            : $"{apiBaseUrl}/";

        _apiBaseUri = new Uri(normalized, UriKind.Absolute);
    }

    public void SetAccessKey(string? accessKey)
    {
        _httpClient.DefaultRequestHeaders.Remove(AccessKeyHeaderName);

        var normalized = accessKey?.Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            _httpClient.DefaultRequestHeaders.Add(AccessKeyHeaderName, normalized);
        }
    }

    public void SetPlatformBaseUrl(string? platformBaseUrl)
    {
        _httpClient.DefaultRequestHeaders.Remove(PlatformBaseUrlHeaderName);

        var normalized = platformBaseUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            _httpClient.DefaultRequestHeaders.Add(PlatformBaseUrlHeaderName, normalized);
        }
    }

    public async Task<CoachSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<CoachSnapshot>(BuildUri("api/snapshot"), cancellationToken)
            ?? new CoachSnapshot();
    }

    public async Task<PlayerProfileDto> GetPlayerProfileAsync(
        string riotId,
        string platformRegion,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/player-profile?riotId={Uri.EscapeDataString(riotId)}&platformRegion={Uri.EscapeDataString(platformRegion)}";
        return await _httpClient.GetFromJsonAsync<PlayerProfileDto>(BuildUri(query), cancellationToken)
            ?? new PlayerProfileDto
            {
                IsConfigured = true,
                IsAvailable = false,
                Message = "Le profil joueur est indisponible pour le moment."
            };
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(BuildUri("healthz"), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private Uri BuildUri(string relativePath) => new(_apiBaseUri, relativePath);

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
