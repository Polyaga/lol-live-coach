using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LolLiveCoach.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LolLiveCoach.Api.Services;

public class RemotePlayerProfileService
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<RemotePlayerProfileService> _logger;
    private readonly IOptionsMonitor<PlatformOptions> _platformOptions;

    public RemotePlayerProfileService(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache memoryCache,
        ILogger<RemotePlayerProfileService> logger,
        IOptionsMonitor<PlatformOptions> platformOptions)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _memoryCache = memoryCache;
        _logger = logger;
        _platformOptions = platformOptions;
        _httpClient.Timeout = TimeSpan.FromSeconds(6);
    }

    public async Task<PlayerProfileResponse> GetProfileAsync(
        string riotId,
        string? platformRegion,
        CancellationToken cancellationToken = default)
    {
        var normalizedRiotId = riotId?.Trim() ?? string.Empty;
        var normalizedPlatformRegion = NormalizePlatform(platformRegion);
        var accessToken = GetCurrentAccessKey();

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return BuildUnavailable(
                normalizedRiotId,
                normalizedPlatformRegion,
                "Connecte ton compte desktop pour charger le profil joueur.");
        }

        var baseUrl = NormalizeBaseUrl(GetCurrentPlatformBaseUrl())
            ?? NormalizeBaseUrl(_platformOptions.CurrentValue.BaseUrl);

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return BuildUnavailable(
                normalizedRiotId,
                normalizedPlatformRegion,
                "La plateforme web n'est pas configuree pour le profil joueur.");
        }

        var cacheKey = $"remote-player-profile:{baseUrl}|{accessToken}|{normalizedRiotId}|{normalizedPlatformRegion}";
        if (_memoryCache.TryGetValue<PlayerProfileResponse>(cacheKey, out var cachedProfile) && cachedProfile is not null)
        {
            return cachedProfile;
        }

        var query = $"api/platform/player-profile?riotId={Uri.EscapeDataString(normalizedRiotId)}&platformRegion={Uri.EscapeDataString(normalizedPlatformRegion)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUrl), query));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return BuildUnavailable(
                    normalizedRiotId,
                    normalizedPlatformRegion,
                    "La session desktop a expire. Reconnecte ton compte.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return BuildUnavailable(
                    normalizedRiotId,
                    normalizedPlatformRegion,
                    $"Le profil joueur distant est indisponible ({(int)response.StatusCode}).");
            }

            var payload = await response.Content.ReadFromJsonAsync<PlayerProfileResponse>(cancellationToken: cancellationToken);
            if (payload is null)
            {
                return BuildUnavailable(
                    normalizedRiotId,
                    normalizedPlatformRegion,
                    "Le profil joueur distant a renvoye une reponse vide.");
            }

            _memoryCache.Set(
                cacheKey,
                payload,
                TimeSpan.FromSeconds(Math.Max(30, _platformOptions.CurrentValue.CacheSeconds)));

            return payload;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load remote Riot profile for {RiotId}", normalizedRiotId);
            return BuildUnavailable(
                normalizedRiotId,
                normalizedPlatformRegion,
                "La plateforme web n'a pas repondu pour le profil joueur.");
        }
    }

    private string? GetCurrentAccessKey()
    {
        var rawValue = _httpContextAccessor.HttpContext?.Request.Headers[SubscriptionAccessService.AccessKeyHeaderName].ToString();
        return string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();
    }

    private string? GetCurrentPlatformBaseUrl()
    {
        var rawValue = _httpContextAccessor.HttpContext?.Request.Headers[SubscriptionAccessService.PlatformBaseUrlHeaderName].ToString();
        return string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();
    }

    private static PlayerProfileResponse BuildUnavailable(
        string riotId,
        string platformRegion,
        string message)
    {
        return new PlayerProfileResponse
        {
            IsConfigured = true,
            IsAvailable = false,
            RiotId = riotId,
            PlatformRegion = platformRegion,
            Message = message
        };
    }

    private static string NormalizePlatform(string? platformRegion)
    {
        var normalized = platformRegion?.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? string.Empty : normalized;
    }

    private static string? NormalizeBaseUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalized = uri.ToString();
        return normalized.EndsWith("/", StringComparison.Ordinal) ? normalized : $"{normalized}/";
    }
}
