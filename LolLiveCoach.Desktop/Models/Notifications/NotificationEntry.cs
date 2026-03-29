namespace LolLiveCoach.Desktop.Models;

public class NotificationEntry
{
    public required DateTime CreatedAt { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string? SubMessage { get; init; }
    public required string SummaryKey { get; init; }
    public List<string> Alerts { get; init; } = [];
}
