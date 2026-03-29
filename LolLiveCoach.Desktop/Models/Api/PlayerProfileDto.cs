namespace LolLiveCoach.Desktop.Models;

public class PlayerProfileDto
{
    public bool IsConfigured { get; set; }
    public bool IsAvailable { get; set; }
    public string Message { get; set; } = string.Empty;
    public string RiotId { get; set; } = string.Empty;
    public string PlatformRegion { get; set; } = string.Empty;
    public int SummonerLevel { get; set; }
    public string SoloQueueTier { get; set; } = "Non classe";
    public int SoloQueueLeaguePoints { get; set; }
    public int SoloQueueWins { get; set; }
    public int SoloQueueLosses { get; set; }
    public double RecentKda { get; set; }
    public double RecentWinRate { get; set; }
    public double RecentCsPerMinute { get; set; }
    public int RecentSampleSize { get; set; }
    public List<PlayerRecentMatchDto> RecentMatches { get; set; } = [];
}

public class PlayerRecentMatchDto
{
    public string MatchId { get; set; } = string.Empty;
    public string ChampionName { get; set; } = string.Empty;
    public string QueueLabel { get; set; } = string.Empty;
    public bool Win { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int Cs { get; set; }
    public int DurationMinutes { get; set; }
    public DateTimeOffset PlayedAt { get; set; }

    public string ResultLabel => Win ? "Victoire" : "Defaite";
    public string ScoreLine => $"{Kills}/{Deaths}/{Assists}";
    public string MetaLine => $"{QueueLabel} - {Cs} CS - {DurationMinutes} min";
    public string PlayedAtLabel => PlayedAt == default ? "-" : PlayedAt.ToLocalTime().ToString("dd/MM HH:mm");
}
