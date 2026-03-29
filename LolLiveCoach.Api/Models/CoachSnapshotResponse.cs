namespace LolLiveCoach.Api.Models;

public class CoachSnapshotResponse
{
    public GameState Game { get; init; } = new();
    public Advice Advice { get; init; } = new();
    public AppAccess Access { get; init; } = new();
}
