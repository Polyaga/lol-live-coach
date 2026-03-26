namespace LolLiveCoach.Api.Models;

public class ActivePlayer
{
    public string? SummonerName { get; set; }
    public int Level { get; set; }
    public double CurrentHealth { get; set; }
    public double MaxHealth { get; set; }
    public double CurrentGold { get; set; }
    public bool IsDead { get; set; }
}