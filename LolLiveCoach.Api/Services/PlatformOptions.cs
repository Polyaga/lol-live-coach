namespace LolLiveCoach.Api.Services;

public class PlatformOptions
{
    public const string SectionName = "Platform";

    public string? BaseUrl { get; set; }
    public int CacheSeconds { get; set; } = 120;
}
