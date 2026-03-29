namespace LolLiveCoach.Api.Models;

public class GameEvent
{
    public int EventId { get; set; }
    public string? EventName { get; set; }
    public double EventTime { get; set; }
    public string? KillerName { get; set; }
    public string? VictimName { get; set; }
    public List<string> Assisters { get; set; } = new();
    public string? TurretKilled { get; set; }
    public string? InhibKilled { get; set; }
    public string? DragonType { get; set; }
    public bool Stolen { get; set; }
    public int KillStreak { get; set; }
    public string? Acer { get; set; }
    public string? AcingTeam { get; set; }
}
