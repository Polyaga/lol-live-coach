namespace LolLiveCoach.Api.Models;

public class Advice
{
    public string MainAdvice { get; set; } = "Aucun conseil disponible.";
    public List<string> Alerts { get; set; } = new();
}