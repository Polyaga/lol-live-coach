namespace LolLiveCoach.Api.Models;

public class BuildAdviceProfile
{
    public string? ResistanceAdvice { get; set; }
    public string? SustainAdvice { get; set; }
    public string? DamagePatternAdvice { get; set; }
    public string? ThreatAdvice { get; set; }
    public List<string> ItemTips { get; set; } = [];
    public List<ItemRecommendation> ItemRecommendations { get; set; } = [];
}
