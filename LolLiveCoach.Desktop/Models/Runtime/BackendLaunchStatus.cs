namespace LolLiveCoach.Desktop.Models;

public class BackendLaunchStatus
{
    public bool IsHealthy { get; init; }
    public bool IsOwnedByDesktop { get; init; }
    public string Title { get; init; } = "Backend";
    public string Detail { get; init; } = string.Empty;
}
