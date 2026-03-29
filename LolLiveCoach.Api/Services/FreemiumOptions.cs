namespace LolLiveCoach.Api.Services;

public class FreemiumOptions
{
    public const string SectionName = "Freemium";

    public string Tier { get; set; } = "Free";
    public bool AllowOverlayPreviewWithoutSubscription { get; set; } = true;
    public string? UpgradeUrl { get; set; }
    public string? ManageSubscriptionUrl { get; set; }
    public List<FreemiumManualOverride> ManualOverrides { get; set; } = [];
}

public class FreemiumManualOverride
{
    public string AccessKey { get; set; } = string.Empty;
    public string Tier { get; set; } = "Premium";
    public string? Label { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}
