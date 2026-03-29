namespace LolLiveCoach.Desktop.Models;

public class CoachSnapshot
{
    public AdviceDto Advice { get; init; } = new();
    public GameStateDto Game { get; init; } = new();
    public SubscriptionAccessDto Access { get; init; } = SubscriptionAccessDto.CreateFree();
}
