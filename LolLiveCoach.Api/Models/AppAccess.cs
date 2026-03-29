namespace LolLiveCoach.Api.Models;

public class AppAccess
{
    public string Tier { get; init; } = "Free";
    public bool HasPremiumAccess { get; init; }
    public bool CanUseOverlayPreview { get; init; } = true;
    public bool CanUseInGameOverlay { get; init; }
    public bool CanSeeDetailedAdvice { get; init; }
    public bool CanSeeDetailedAlerts { get; init; }
    public bool CanUseNotificationBubbles { get; init; }
    public bool CanUseNotificationHistory { get; init; }
    public string StatusTitle { get; init; } = "Mode Free";
    public string StatusMessage { get; init; } =
        "Le tableau de bord reste disponible. L'overlay live et les alertes avancees demandent un abonnement.";
    public string? UpgradeUrl { get; init; }
    public string? ManageSubscriptionUrl { get; init; }
}
