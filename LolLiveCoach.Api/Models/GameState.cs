namespace LolLiveCoach.Api.Models;

public class GameState
{
    public bool IsInGame { get; set; }
    public string? GameMode { get; set; }
    public double GameTimeSeconds { get; set; }

    public ActivePlayer? ActivePlayer { get; set; }

    public string? LocalPlayerTeam { get; set; }

    public List<PlayerSummary> AllPlayers { get; set; } = new();
    public List<PlayerSummary> Allies { get; set; } = new();
    public List<PlayerSummary> Enemies { get; set; } = new();

    public List<GameEvent> Events { get; set; } = new();

    public PlayerRole DetectedRole { get; set; } = PlayerRole.Unknown;

