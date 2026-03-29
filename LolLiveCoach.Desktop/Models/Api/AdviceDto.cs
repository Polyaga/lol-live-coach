namespace LolLiveCoach.Desktop.Models;

public class AdviceDto
{
    public string MainAdvice { get; set; } = "Aucun conseil disponible.";
    public string? SecondaryAdvice { get; set; }
    public string? GamePhase { get; set; }
    public int Priority { get; set; }
    public List<string>? Alerts { get; set; }
    public List<string>? BuildTips { get; set; }
    public List<ItemRecommendationDto>? ItemRecommendations { get; set; }
}
