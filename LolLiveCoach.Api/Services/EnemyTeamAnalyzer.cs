using LolLiveCoach.Api.Models;

namespace LolLiveCoach.Api.Services;

public class EnemyTeamAnalyzer
{
    private static readonly HashSet<string> AdChampions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tryndamere", "Pantheon", "Hecarim", "Xayah", "Jinx", "Caitlyn", "Draven", "Lucian",
        "Samira", "Nilah", "Zed", "Talon", "Yasuo", "Yone", "Riven", "Darius", "Sett",
        "Renekton", "Lee Sin", "Nocturne", "Kayn", "Master Yi", "Vi", "Jarvan IV", "Graves"
    };

    private static readonly HashSet<string> ApChampions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Brand", "Karthus", "Ziggs", "Lux", "Syndra", "Ahri", "Annie", "Veigar", "Viktor",
        "Cassiopeia", "Malzahar", "Xerath", "Orianna", "Lissandra", "Swain", "Teemo",
        "Morgana", "Kennen", "Fiddlesticks", "Evelynn", "Elise", "Diana", "Ekko", "Gragas"
    };

    private static readonly HashSet<string> SustainChampions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Aatrox", "Vladimir", "Soraka", "Dr. Mundo", "Warwick", "Sylas", "Briar",
        "Swain", "Olaf", "Hecarim", "Yuumi", "Nilah", "Tryndamere"
    };

    private static readonly HashSet<string> FrontlineChampions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Braum", "Leona", "Nautilus", "Alistar", "Dr. Mundo", "Ornn", "Sion", "Malphite",
        "Cho'Gath", "Sejuani", "Zac", "Rammus", "Volibear", "Maokai", "Shen", "Amumu"
    };

    private static readonly HashSet<string> EngageChampions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Nocturne", "Hecarim", "Vi", "Jarvan IV", "Malphite", "Zac", "Sejuani", "Rammus",
        "Leona", "Nautilus", "Alistar", "Amumu", "Diana", "Fiddlesticks", "Ornn"
    };

    private static readonly HashSet<string> PickChampions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Blitzcrank", "Thresh", "Pyke", "Morgana", "Lux", "Ahri", "Syndra", "Annie",
        "Elise", "Evelynn", "Nidalee", "Zoe", "Neeko", "Rengar"
    };

    public EnemyTeamProfile Analyze(IEnumerable<PlayerSummary> enemies)
    {
        var enemyList = enemies.ToList();

        return new EnemyTeamProfile
        {
            AdThreatCount = enemyList.Count(e =>
                !string.IsNullOrWhiteSpace(e.ChampionName) && AdChampions.Contains(e.ChampionName)),

            ApThreatCount = enemyList.Count(e =>
                !string.IsNullOrWhiteSpace(e.ChampionName) && ApChampions.Contains(e.ChampionName)),

            SustainCount = enemyList.Count(e =>
                !string.IsNullOrWhiteSpace(e.ChampionName) && SustainChampions.Contains(e.ChampionName)),

            FrontlineCount = enemyList.Count(e =>
                !string.IsNullOrWhiteSpace(e.ChampionName) && FrontlineChampions.Contains(e.ChampionName)),

            EngageCount = enemyList.Count(e =>
                !string.IsNullOrWhiteSpace(e.ChampionName) && EngageChampions.Contains(e.ChampionName)),

            PickCount = enemyList.Count(e =>
                !string.IsNullOrWhiteSpace(e.ChampionName) && PickChampions.Contains(e.ChampionName))
        };
    }
}
