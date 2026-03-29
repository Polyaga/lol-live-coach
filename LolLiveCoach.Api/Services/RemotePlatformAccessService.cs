using System.Net.Http.Headers;
using System.Net.Http.Json;
using LolLiveCoach.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LolLiveCoach.Api.Services;

public class RemotePlatformAccessService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly IOptionsMonitor<PlatformOptions> _platformOptions;

    public RemotePlatformAccessService(
        HttpClient httpClient,
        IMemoryCache memoryCache,
        IOptionsMonitor<PlatformOptions> platformOptions)
    {
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _platformOptions = platformOptions;
        _httpClient.Timeout = TimeSpan.FromSeconds(4);
    }

    public async Task<AppAccess?> GetAccessAsync(
        string accessToken,
        string? platformBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var normalizedToken = accessToken.Trim();
        var baseUrl = NormalizeBaseUrl(platformBaseUrl) ?? NormalizeBaseUrl(_platformOptions.CurrentValue.BaseUrl);

        if (string.IsNullOrWhiteSpace(normalizedToken) || string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        var cacheKey = $"{baseUrl}|{normalizedToken}";
        if (_memoryCache.TryGetValue<AppAccess>(cacheKey, out var cachedAccess))
        {
            return cachedAccess;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUrl), "api/platform/access"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", normalizedToken);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<PlatformAccessEnvelope>(cancellationToken: cancellationToken);
            if (payload?.Access is null)
            {
                return null;
            }

            _memoryCache.Set(cacheKey, payload.Access, TimeSpan.FromSeconds(Math.Max(30, _platformOptions.CurrentValue.CacheSeconds)));
            return payload.Access;
        }
        catch
        {
            return null;
        }
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
