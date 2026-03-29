namespace LolLiveCoach.Desktop.Models;

public class PlayerSummaryDto
{
    public string? SummonerName { get; set; }
    public string? ChampionName { get; set; }
    public string? Team { get; set; }
    public bool IsBot { get; set; }
    public int Level { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
}
