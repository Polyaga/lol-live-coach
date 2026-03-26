namespace LolLiveCoach.Api.Models;

public class EnemyTeamProfile
{
    public int AdThreatCount { get; set; }
    public int ApThreatCount { get; set; }
    public int SustainCount { get; set; }
    public int FrontlineCount { get; set; }

    public bool IsMostlyAd => AdThreatCount >= 3 && AdThreatCount > ApThreatCount;
    public bool IsMostlyAp => ApThreatCount >= 3 && ApThreatCount > AdThreatCount;
    public bool IsMixed => AdThreatCount >= 2 && ApThreatCount >= 2;
}