namespace LolLiveCoach.Desktop.Models;

public class GameStateDto
{
    public bool IsInGame { get; set; }
    public string? GameMode { get; set; }
    public double GameTimeSeconds { get; set; }
    public ActivePlayerDto? ActivePlayer { get; set; }
    public PlayerSummaryDto? LocalPlayer { get; set; }
    public int DetectedRole { get; set; }
}
