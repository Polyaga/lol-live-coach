using LolLiveCoach.Desktop.Services;

namespace LolLiveCoach.Desktop.Models;

public class OverlaySettings
{
    public const string DefaultApiBaseUrl = "http://localhost:5004";
    public static string DefaultPlatformBaseUrl =>
        AppBuildMetadata.GetValue("PlatformBaseUrl") ?? "http://localhost:3000";

    public string ApiBaseUrl { get; set; } = DefaultApiBaseUrl;
    public string PlatformBaseUrl { get; set; } = DefaultPlatformBaseUrl;
    public string AccountEmail { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string PreferredRiotId { get; set; } = string.Empty;
    public string PreferredPlatformRegion { get; set; } = "EUW1";
    public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.BottomRight;
    public bool AutoStartLocalBackend { get; set; } = true;
    public double? OverlayLeft { get; set; }
    public double? OverlayTop { get; set; }
    public double? HistoryLeft { get; set; }
    public double? HistoryTop { get; set; }
    public double? BuildLeft { get; set; }
    public double? BuildTop { get; set; }

    public static OverlaySettings CreateDefault() => new();
}
