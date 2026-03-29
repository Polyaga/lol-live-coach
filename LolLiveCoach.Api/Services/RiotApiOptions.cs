namespace LolLiveCoach.Api.Services;

public class RiotApiOptions
{
    public const string SectionName = "RiotApi";

    public string ApiKey { get; set; } = string.Empty;
    public string DefaultPlatformRegion { get; set; } = "EUW1";
    public int RecentMatchesCount { get; set; } = 5;
    public int CacheMinutes { get; set; } = 2;
}
