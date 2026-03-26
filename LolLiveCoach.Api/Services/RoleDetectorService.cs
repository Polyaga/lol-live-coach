using LolLiveCoach.Api.Models;

namespace LolLiveCoach.Api.Services;

public class RoleDetectorService
{
    private static readonly HashSet<int> JungleItemIds = new()
    {
        1101, 1102, 1103
    };

    private static readonly HashSet<int> SupportItemIds = new()
    {
        3865, 3866, 3867, 3869, 3870, 3871, 3876
    };

    private static readonly HashSet<string> SupportChampions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Leona", "Nautilus", "Alistar", "Thresh", "Zyra", "Morgana", "Yuumi",
        "Lulu", "Bard", "Karma", "Nami", "Janna", "Rakan", "Pyke",
        "Seraphine", "Senna", "Tahm Kench", "Zilean", "Blitzcrank",
        "Braum", "Sona", "Soraka", "Renata Glasc", "Milio"
    };

    private static readonly HashSet<string> AdcChampions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Caitlyn", "Draven", "Jinx", "Lucian", "Samira", "Nilah", "Xayah",
        "Kai'Sa", "Ashe", "Ezreal", "Aphelios", "Miss Fortune", "Sivir",
        "Twitch", "Kog'Maw", "Varus", "Zeri", "Tristana", "Vayne"
    };

    private static readonly HashSet<string> MidChampions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ahri", "Syndra", "Orianna", "Viktor", "Zed", "Talon", "Yasuo",
        "Yone", "Veigar", "Lux", "Annie", "Cassiopeia", "Akali",
        "LeBlanc", "Azir", "Sylas", "Vex", "Twisted Fate", "Katarina"
    };

    private static readonly HashSet<string> JungleChampions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Lee Sin", "Nocturne", "Kayn", "Master Yi", "Vi", "Jarvan IV", "Graves",
        "Elise", "Nidalee", "Ekko", "Kha'Zix", "Sejuani", "Taliyah", "Nunu",
        "Udyr", "Volibear", "Warwick", "Rek'Sai", "Rammus", "Shaco",
        "Shyvana", "Briar", "Hecarim", "Bel'Veth", "Viego", "Amumu",
        "Zac", "Lillia", "Fiddlesticks", "Ivern", "Xin Zhao"
    };

    private static readonly HashSet<string> TopChampions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Riven", "Darius", "Sett", "Renekton", "Ornn", "Sion", "Malphite",
        "Shen", "Dr. Mundo", "Garen", "Aatrox", "Camille", "Fiora",
        "Illaoi", "Mordekaiser", "Nasus", "Olaf", "Tryndamere", "Volibear"
    };

    public PlayerRole DetectRole(PlayerSummary player)
    {
        if (player is null)
            return PlayerRole.Unknown;

        // 1. Détection forte par item jungle
        if (player.Items.Any(item => JungleItemIds.Contains(item.ItemId)))
            return PlayerRole.Jungle;

        // 2. Détection forte par item support
        if (player.Items.Any(item => SupportItemIds.Contains(item.ItemId)))
            return PlayerRole.Support;

        // 3. Fallback champion pool
        if (!string.IsNullOrWhiteSpace(player.ChampionName))
        {
            if (JungleChampions.Contains(player.ChampionName))
                return PlayerRole.Jungle;

            if (SupportChampions.Contains(player.ChampionName))
                return PlayerRole.Support;

            if (AdcChampions.Contains(player.ChampionName))
                return PlayerRole.Adc;

            if (MidChampions.Contains(player.ChampionName))
                return PlayerRole.Mid;

            if (TopChampions.Contains(player.ChampionName))
                return PlayerRole.Top;
        }

        return PlayerRole.Unknown;
    }
}