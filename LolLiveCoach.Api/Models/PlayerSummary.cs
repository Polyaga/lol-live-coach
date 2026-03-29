namespace LolLiveCoach.Api.Models;

public class PlayerSummary
{
    public string? SummonerName { get; set; }
    public string? ChampionName { get; set; }
    public string? Team { get; set; }
    public string? Position { get; set; }
    public bool IsBot { get; set; }
    public bool IsDead { get; set; }
    public double RespawnTimer { get; set; }
    public int Level { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int CreepScore { get; set; }
    public double WardScore { get; set; }
    public List<ItemSummary> Items { get; set; } = new();
}
