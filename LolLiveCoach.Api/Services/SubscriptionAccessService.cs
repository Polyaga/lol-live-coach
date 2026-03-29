using LolLiveCoach.Api.Models;
using Microsoft.Extensions.Options;

namespace LolLiveCoach.Api.Services;

public class SubscriptionAccessService
{
    public const string AccessKeyHeaderName = "X-Lol-Access-Key";
    public const string PlatformBaseUrlHeaderName = "X-Lol-Platform-Url";
    private const string FreeTier = "Free";
    private const string PremiumTier = "Premium";
    private readonly IOptionsMonitor<FreemiumOptions> _freemiumOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RemotePlatformAccessService _remotePlatformAccessService;

    public SubscriptionAccessService(
        IOptionsMonitor<FreemiumOptions> freemiumOptions,
        IHttpContextAccessor httpContextAccessor,
        RemotePlatformAccessService remotePlatformAccessService)
    {
        _freemiumOptions = freemiumOptions;
        _httpContextAccessor = httpContextAccessor;
        _remotePlatformAccessService = remotePlatformAccessService;
    }

    public async Task<AppAccess> GetCurrentAccessAsync(CancellationToken cancellationToken = default)
    {
        var options = _freemiumOptions.CurrentValue;
        var accessKey = GetCurrentAccessKey();
        var manualOverride = FindActiveOverride(options, accessKey);
        if (manualOverride is not null)
        {
            return BuildAccess(
                manualOverride.Tier,
                options,
                statusTitle: IsPremiumTier(manualOverride.Tier) ? "Premium manuel actif" : "Mode Free force manuellement",
                statusMessage: BuildOverrideMessage(manualOverride));
        }

        if (!string.IsNullOrWhiteSpace(accessKey))
        {
            var remoteAccess = await _remotePlatformAccessService.GetAccessAsync(
                accessKey,
                GetCurrentPlatformBaseUrl(),
                cancellationToken);

            if (remoteAccess is not null)
            {
                return remoteAccess;
            }
        }

        return BuildAccess(options.Tier, options);
    }

    public Advice ApplyAdviceEntitlements(Advice advice, AppAccess access)
    {
        if (access.HasPremiumAccess)
        {
            return advice;
        }

        return new Advice
        {
            MainAdvice = advice.MainAdvice,
            SecondaryAdvice = null,
            GamePhase = advice.GamePhase,
            Priority = advice.Priority,
            Alerts = [],
            BuildTips = [],
            ItemRecommendations = []
        };
    }

    private static string? SanitizeUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? uri.ToString()
            : null;
    }

    private AppAccess BuildAccess(
        string? tier,
        FreemiumOptions options,
        string? statusTitle = null,
        string? statusMessage = null)
    {
        if (IsPremiumTier(tier))
        {
            return new AppAccess
            {
                Tier = PremiumTier,
                HasPremiumAccess = true,
                CanUseOverlayPreview = true,
                CanUseInGameOverlay = true,
                CanSeeDetailedAdvice = true,
                CanSeeDetailedAlerts = true,
                CanUseNotificationBubbles = true,
                CanUseNotificationHistory = true,
                StatusTitle = statusTitle ?? "Abonnement Premium actif",
                StatusMessage = statusMessage ?? "L'overlay in-game, les alertes detaillees et l'historique live sont debloques.",
                UpgradeUrl = SanitizeUrl(options.UpgradeUrl),
                ManageSubscriptionUrl = SanitizeUrl(options.ManageSubscriptionUrl)
            };
        }

        return new AppAccess
        {
            Tier = FreeTier,
            HasPremiumAccess = false,
            CanUseOverlayPreview = options.AllowOverlayPreviewWithoutSubscription,
            CanUseInGameOverlay = false,
            CanSeeDetailedAdvice = false,
            CanSeeDetailedAlerts = false,
            CanUseNotificationBubbles = false,
            CanUseNotificationHistory = false,
            StatusTitle = statusTitle ?? "Mode Free actif",
            StatusMessage = statusMessage ?? "Le tableau de bord et le conseil principal restent disponibles. L'overlay in-game et les alertes detaillees sont reserves au premium.",
            UpgradeUrl = SanitizeUrl(options.UpgradeUrl),
            ManageSubscriptionUrl = SanitizeUrl(options.ManageSubscriptionUrl)
        };
    }

    private string? GetCurrentAccessKey()
    {
        var rawValue = _httpContextAccessor.HttpContext?.Request.Headers[AccessKeyHeaderName].ToString();
        return string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();
    }

    private string? GetCurrentPlatformBaseUrl()
    {
        var rawValue = _httpContextAccessor.HttpContext?.Request.Headers[PlatformBaseUrlHeaderName].ToString();
        return SanitizeUrl(string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim());
    }

    private static FreemiumManualOverride? FindActiveOverride(FreemiumOptions options, string? accessKey)
    {
        if (string.IsNullOrWhiteSpace(accessKey) || options.ManualOverrides.Count == 0)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        return options.ManualOverrides.FirstOrDefault(overrideEntry =>
            !string.IsNullOrWhiteSpace(overrideEntry.AccessKey)
            && string.Equals(overrideEntry.AccessKey.Trim(), accessKey, StringComparison.OrdinalIgnoreCase)
            && (overrideEntry.ExpiresAtUtc is null || overrideEntry.ExpiresAtUtc.Value >= now));
    }

    private static string BuildOverrideMessage(FreemiumManualOverride overrideEntry)
    {
        var label = string.IsNullOrWhiteSpace(overrideEntry.Label)
            ? overrideEntry.AccessKey
            : overrideEntry.Label;
        var reason = string.IsNullOrWhiteSpace(overrideEntry.Reason)
            ? "Override manuel applique depuis la configuration."
            : overrideEntry.Reason.Trim();
        var expiry = overrideEntry.ExpiresAtUtc is null
            ? "Sans date de fin."
            : $"Valide jusqu'au {overrideEntry.ExpiresAtUtc.Value:yyyy-MM-dd HH:mm} UTC.";

        return $"{label} : {reason} {expiry}";
    }

    private static bool IsPremiumTier(string? tier)
    {
        return string.Equals(tier, PremiumTier, StringComparison.OrdinalIgnoreCase);
    }
}
