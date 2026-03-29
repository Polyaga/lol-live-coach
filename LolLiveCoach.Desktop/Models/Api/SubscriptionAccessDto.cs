namespace LolLiveCoach.Desktop.Models;

public class SubscriptionAccessDto
{
    public string Tier { get; set; } = "Free";
    public bool HasPremiumAccess { get; set; }
    public bool CanUseOverlayPreview { get; set; } = true;
    public bool CanUseInGameOverlay { get; set; }
    public bool CanSeeDetailedAdvice { get; set; }
    public bool CanSeeDetailedAlerts { get; set; }
    public bool CanUseNotificationBubbles { get; set; }
    public bool CanUseNotificationHistory { get; set; }
    public string StatusTitle { get; set; } = "Mode Free";
    public string StatusMessage { get; set; } =
        "Le tableau de bord reste disponible. L'overlay live et les alertes avancees demandent un abonnement.";
    public string? UpgradeUrl { get; set; }
    public string? ManageSubscriptionUrl { get; set; }

    public static SubscriptionAccessDto CreateFree() => new();
}
