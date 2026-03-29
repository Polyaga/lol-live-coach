using System.Collections.ObjectModel;
using LolLiveCoach.Desktop.Models;

namespace LolLiveCoach.Desktop.Services;

public class NotificationFeed
{
    private const int MaxEntries = 40;
    private const int NotificationPriorityThreshold = 2;
    private string? _lastSummaryKey;

    public ObservableCollection<NotificationEntry> Entries { get; } = [];

    public NotificationEntry? PushIfChanged(CoachSnapshot snapshot)
    {
        if (!snapshot.Game.IsInGame)
        {
            _lastSummaryKey = null;
            return null;
        }

        var entry = BuildEntry(snapshot);
        if (entry is null)
        {
            return null;
        }

        if (entry.SummaryKey == _lastSummaryKey)
        {
            return null;
        }

        _lastSummaryKey = entry.SummaryKey;
        Entries.Insert(0, entry);

        while (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }

        return entry;
    }

    public void Clear()
    {
        _lastSummaryKey = null;
        Entries.Clear();
    }

    private static NotificationEntry? BuildEntry(CoachSnapshot snapshot)
    {
        var advice = snapshot.Advice;
        var alerts = advice.Alerts?.Where(alert => !string.IsNullOrWhiteSpace(alert)).ToList() ?? [];
        var meaningfulAlerts = alerts
            .Where(alert => !IsRoutineObjectiveAlert(alert))
            .ToList();
        var mainAdvice = advice.MainAdvice.Trim();

        if (IsLowSignal(advice.Priority, mainAdvice, meaningfulAlerts))
        {
            return null;
        }

        var subMessage = meaningfulAlerts.FirstOrDefault();
        if (string.Equals(subMessage, mainAdvice, StringComparison.OrdinalIgnoreCase))
        {
            subMessage = null;
        }

        return new NotificationEntry
        {
            CreatedAt = DateTime.Now,
            Title = DateTime.Now.ToString("HH:mm:ss"),
            Message = mainAdvice,
            SubMessage = subMessage,
            Alerts = meaningfulAlerts,
            SummaryKey = string.Join("||",
                mainAdvice,
                subMessage ?? string.Empty,
                string.Join("|", meaningfulAlerts.Take(2)))
        };
    }

    private static bool IsLowSignal(int priority, string mainAdvice, List<string> alerts)
    {
        if (IsRoutineObjectiveMessage(mainAdvice) && priority < 3 && alerts.Count == 0)
        {
            return true;
        }

        if (priority < NotificationPriorityThreshold && alerts.Count == 0)
        {
            return true;
        }

        if (alerts.Count > 0)
        {
            return false;
        }

        return mainAdvice.StartsWith("Etat stable", StringComparison.OrdinalIgnoreCase)
            || mainAdvice.StartsWith("Pas d'alerte majeure", StringComparison.OrdinalIgnoreCase)
            || mainAdvice.StartsWith("Aucune partie", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRoutineObjectiveAlert(string alert)
    {
        return alert.StartsWith("Dragon disponible", StringComparison.OrdinalIgnoreCase)
            || alert.StartsWith("Elder disponible", StringComparison.OrdinalIgnoreCase)
            || alert.StartsWith("Nashor disponible", StringComparison.OrdinalIgnoreCase)
            || alert.StartsWith("Herald disponible", StringComparison.OrdinalIgnoreCase)
            || alert.StartsWith("Grubs disponible", StringComparison.OrdinalIgnoreCase)
            || alert.StartsWith("Dragon dans ", StringComparison.OrdinalIgnoreCase)
            || alert.StartsWith("Elder dans ", StringComparison.OrdinalIgnoreCase)
            || alert.StartsWith("Nashor dans ", StringComparison.OrdinalIgnoreCase)
            || alert.StartsWith("Herald dans ", StringComparison.OrdinalIgnoreCase)
            || alert.StartsWith("Grubs dans ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRoutineObjectiveMessage(string mainAdvice)
    {
        return mainAdvice.StartsWith("Path vers ", StringComparison.OrdinalIgnoreCase)
            || mainAdvice.StartsWith("Push mid puis bouge", StringComparison.OrdinalIgnoreCase)
            || mainAdvice.StartsWith("Reset wards pour ", StringComparison.OrdinalIgnoreCase)
            || mainAdvice.StartsWith("Prepare le timing ", StringComparison.OrdinalIgnoreCase)
            || mainAdvice.StartsWith("Raccourcis ta side avant ", StringComparison.OrdinalIgnoreCase)
            || mainAdvice.StartsWith("Prepare ", StringComparison.OrdinalIgnoreCase)
            || mainAdvice.StartsWith("Joue autour de ", StringComparison.OrdinalIgnoreCase)
            || mainAdvice.StartsWith("Garde la priorite mid pour ", StringComparison.OrdinalIgnoreCase)
            || mainAdvice.StartsWith("Prends la vision de ", StringComparison.OrdinalIgnoreCase)
            || mainAdvice.StartsWith("Reste connecte a ", StringComparison.OrdinalIgnoreCase)
            || mainAdvice.StartsWith("Respecte la zone de ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mainAdvice, "Ne side pas trop loin.", StringComparison.OrdinalIgnoreCase);
    }
}
