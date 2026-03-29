namespace LolLiveCoach.Api.Models;

public class Advice
{
    public string MainAdvice { get; set; } = "Aucun conseil disponible.";
    public string? SecondaryAdvice { get; set; }
    public string? GamePhase { get; set; }
    public int Priority { get; set; }
    public List<string> Alerts { get; set; } = new();
    public List<string> BuildTips { get; set; } = new();
    public List<ItemRecommendation> ItemRecommendations { get; set; } = new();
}
