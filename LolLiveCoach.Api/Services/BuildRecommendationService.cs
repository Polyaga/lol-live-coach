using LolLiveCoach.Api.Models;

namespace LolLiveCoach.Api.Services;

public class BuildRecommendationService
{
    private static readonly HashSet<string> TankChampions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Alistar", "Amumu", "Braum", "Cho'Gath", "Dr. Mundo", "Leona", "Maokai",
        "Malphite", "Nautilus", "Nunu", "Nunu & Willump", "Ornn", "Rammus",
        "Sejuani", "Shen", "Sion", "Volibear", "Willump", "Zac"
    };

    private static readonly HashSet<string> FrontlineChampions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Alistar", "Amumu", "Braum", "Cho'Gath", "Leona", "Malphite", "Maokai",
        "Nautilus", "Nunu", "Nunu & Willump", "Ornn", "Rammus", "Sejuani", "Shen",
        "Sion", "Volibear", "Willump", "Zac"
    };

    private static readonly HashSet<string> ApChampions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ahri", "Anivia", "Annie", "Aurelion Sol", "Brand", "Cassiopeia", "Diana", "Ekko",
        "Elise", "Evelynn", "Fiddlesticks", "Gragas", "Karthus", "Kennen", "LeBlanc", "Lissandra",
        "Lux", "Malzahar", "Morgana", "Orianna", "Swain", "Syndra", "Taliyah", "Teemo",
        "Twisted Fate", "Veigar", "Vex", "Viktor", "Xerath", "Ziggs", "Zoe"
    };

    private static readonly HashSet<string> AdChampions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Aatrox", "Caitlyn", "Darius", "Draven", "Ezreal", "Fiora", "Gangplank", "Graves",
        "Hecarim", "Jarvan IV", "Jhin", "Jinx", "Kayn", "Kha'Zix", "Lee Sin", "Lucian",
        "Master Yi", "Miss Fortune", "Nilah", "Nocturne", "Pantheon", "Renekton", "Rengar",
        "Riven", "Samira", "Sett", "Talon", "Tryndamere", "Vayne", "Vi", "Xayah",
        "Yasuo", "Yone", "Zed"
    };

    private static readonly HashSet<int> AntiHealItemIds =
    [
        3123, 3033, 3916, 3165, 3076, 3193, 6609
    ];

    private static readonly HashSet<int> ArmorItemIds =
    [
        1031, 3047, 3110, 3143, 3157, 3742
    ];

    private static readonly HashSet<int> MagicResistItemIds =
    [
        1057, 2504, 3111, 3155, 3156, 3065, 3102, 3105, 3001, 3091, 4401
    ];

    private static readonly HashSet<int> AntiTankItemIds =
    [
        3035, 3036, 3071, 3135, 6653
    ];

    private static readonly HashSet<int> CritItemIds =
    [
        3031, 3072, 3085, 3094, 3036, 3033, 6671, 6672
    ];

    private static readonly HashSet<int> ApThreatItemIds =
    [
        3089, 3100, 3135, 4637, 4645, 6653, 6655
    ];

    private static readonly HashSet<int> SustainItemIds =
    [
        3072, 3153, 3074, 6673, 4633
    ];

    private static readonly HashSet<int> QssItemIds =
    [
        3140, 3139, 6035
    ];

    private static readonly HashSet<int> StasisItemIds =
    [
        3157
    ];

    private static readonly HashSet<int> CompletedBootItemIds =
    [
        3005, 3006, 3009, 3010, 3020, 3047, 3111, 3117, 3158, 2422
    ];

    private static readonly HashSet<int> HoldableComponentItemIds =
    [
        3076, 3123, 3140, 3916
    ];

    private readonly RiotItemCatalogService _riotItemCatalogService;

    public BuildRecommendationService(RiotItemCatalogService riotItemCatalogService)
    {
        _riotItemCatalogService = riotItemCatalogService;
    }

    public async Task<BuildAdviceProfile> BuildAsync(
        GameState gameState,
        EnemyTeamProfile enemyProfile,
        CancellationToken cancellationToken = default)
    {
        var result = new BuildAdviceProfile();
        var player = gameState.LocalPlayer;

        if (player is null)
        {
            return result;
        }

        var catalog = await _riotItemCatalogService.GetItemCatalogAsync(cancellationToken);
        var alliesWithoutPlayer = gameState.Allies
            .Where(ally => !IsSamePlayer(ally.SummonerName, player.SummonerName))
            .ToList();
        var role = gameState.DetectedRole;
        var archetype = InferArchetype(player, role, catalog);
        var currentGold = gameState.ActivePlayer?.CurrentGold ?? 0;
        var playerItemIds = player.Items
            .Where(item => item.ItemId > 0)
            .Select(item => item.ItemId)
            .ToHashSet();

        var context = new ItemizationContext(
            gameState,
            player,
            role,
            archetype,
            catalog,
            playerItemIds,
            BuildTeamCoverage(alliesWithoutPlayer, catalog),
            BuildTeamCoverage(gameState.Allies, catalog),
            BuildThreatSignals(gameState, enemyProfile, catalog),
            enemyProfile,
            currentGold);

        result.ResistanceAdvice = BuildResistanceAdvice(context);
        result.SustainAdvice = BuildSustainAdvice(context);
        result.DamagePatternAdvice = BuildDamagePatternAdvice(context);
        result.ThreatAdvice = BuildThreatAdvice(context);

        var itemRecommendations = BuildItemRecommendations(context);
        result.ItemRecommendations = itemRecommendations;
        result.ItemTips = itemRecommendations
            .Select(FormatItemTip)
            .Distinct()
            .Take(4)
            .ToList();

        return result;
    }

    private static List<ItemRecommendation> BuildItemRecommendations(ItemizationContext context)
    {
        var recommendations = new List<ItemRecommendation>();

        AddAntiHealRecommendation(recommendations, context);
        AddAntiTankRecommendation(recommendations, context);
        AddArmorRecommendation(recommendations, context);
        AddMagicResistRecommendation(recommendations, context);
        AddSurvivalRecommendation(recommendations, context);
        AddBootsRecommendation(recommendations, context);

        return recommendations
            .GroupBy(recommendation => recommendation.ItemId)
            .Select(group => group
                .OrderByDescending(recommendation => recommendation.Priority)
                .First())
            .OrderByDescending(recommendation => recommendation.Priority)
            .ThenBy(recommendation => recommendation.ItemName)
            .Take(3)
            .ToList();
    }

    private static void AddAntiHealRecommendation(List<ItemRecommendation> recommendations, ItemizationContext context)
    {
        if (context.Threats.SustainPressure < 2 || HasAny(context.PlayerItemIds, AntiHealItemIds))
        {
            return;
        }

        var template = SelectAntiHealItem(context);
        var reason = context.AllyCoverage.AntiHealCount == 0
            ? "Le sustain ennemi monte et aucun allie n'a encore apporte de vraie source d'anti-heal."
            : "Le sustain ennemi reste assez haut pour que tu ajoutes ta propre source d'anti-heal.";
        var priority = context.AllyCoverage.AntiHealCount == 0 ? 96 : 84;

        recommendations.Add(BuildRecommendation(context, template, "Anti-heal", reason, priority));
    }

    private static void AddAntiTankRecommendation(List<ItemRecommendation> recommendations, ItemizationContext context)
    {
        var enemyHasRealFrontline = context.Threats.FrontlinePressure >= 2
            || context.Threats.ArmorStackCount >= 2
            || context.Threats.MagicResistStackCount >= 2;

        if (!enemyHasRealFrontline || HasAny(context.PlayerItemIds, AntiTankItemIds))
        {
            return;
        }

        var template = SelectAntiTankItem(context);
        var reason = context.AllyCoverage.AntiTankCount == 0
            ? "Leur frontline s'installe et ton equipe manque encore de shred pour les fights longs."
            : "Leur frontline rallonge deja les fights, ajoute ta propre couche d'anti-tank.";
        var priority = context.AllyCoverage.AntiTankCount == 0 ? 90 : 78;

        recommendations.Add(BuildRecommendation(context, template, "Anti-tank", reason, priority));
    }

    private static void AddArmorRecommendation(List<ItemRecommendation> recommendations, ItemizationContext context)
    {
        var needsArmor = context.EnemyProfile.IsMostlyAd
            || context.Threats.PhysicalPressure >= context.Threats.MagicPressure + 2
            || context.Threats.CritPressure >= 2;
        var armorPieces = CountTraitPieces(context.Player, context.Catalog, ArmorItemIds, "Armor");

        if (!needsArmor || armorPieces >= 2)
        {
            return;
        }

        var template = SelectArmorItem(context);
        var reason = context.TeamCoverage.FrontlineCount == 0 && context.Role is PlayerRole.Top or PlayerRole.Jungle or PlayerRole.Support
            ? "La pression AD est forte et votre compo est legere devant, donc ton slot armure a encore plus de valeur."
            : context.Threats.CritPressure >= 2
                ? "Les carries adverses commencent deja a crit, il faut une reponse armure rapidement."
                : "La pression physique adverse domine deja les prochains fights.";
        var priority = context.Threats.CritPressure >= 2 ? 88 : 80;

        recommendations.Add(BuildRecommendation(context, template, "Armure", reason, priority));
    }

    private static void AddMagicResistRecommendation(List<ItemRecommendation> recommendations, ItemizationContext context)
    {
        var needsMagicResist = context.EnemyProfile.IsMostlyAp
            || context.Threats.MagicPressure >= context.Threats.PhysicalPressure + 2
            || (context.EnemyProfile.HasHighPickThreat && context.Threats.MagicPressure >= 3);
        var magicResistPieces = CountTraitPieces(context.Player, context.Catalog, MagicResistItemIds, "SpellBlock");

        if (!needsMagicResist || magicResistPieces >= 2)
        {
            return;
        }

        var template = SelectMagicResistItem(context);
        var reason = context.EnemyProfile.HasHighPickThreat
            ? "Le pick magique en face devient une vraie menace. Rajoute une marge d'erreur avant le prochain catch."
            : "La pression AP adverse monte et ta RM manque encore pour tenir le premier impact.";
        var priority = context.EnemyProfile.HasHighPickThreat ? 86 : 80;

        recommendations.Add(BuildRecommendation(context, template, "Resistance magique", reason, priority));
    }

    private static void AddSurvivalRecommendation(List<ItemRecommendation> recommendations, ItemizationContext context)
    {
        if ((!context.EnemyProfile.HasHighPickThreat && !context.EnemyProfile.HasHeavyEngage)
            || HasAny(context.PlayerItemIds, QssItemIds)
            || HasAny(context.PlayerItemIds, StasisItemIds))
        {
            return;
        }

        var template = SelectSurvivalItem(context);
        var reason = context.EnemyProfile.HasHighPickThreat
            ? "Ils jouent beaucoup le catch. Un slot de survie t'evite de perdre le fight avant qu'il commence."
            : "Leur engage ferme vite les fights. Il te faut une vraie sortie de secours sur le premier contact.";
        var priority = context.EnemyProfile.HasHighPickThreat ? 84 : 74;

        recommendations.Add(BuildRecommendation(context, template, "Survie", reason, priority));
    }

    private static void AddBootsRecommendation(List<ItemRecommendation> recommendations, ItemizationContext context)
    {
        if (HasCompletedBoots(context.Player, context.Catalog) || context.GameState.GameTimeSeconds < 420)
        {
            return;
        }

        var template = SelectBootsItem(context);
        var reason = context.EnemyProfile.IsMostlyAd
            ? "Tu n'as pas encore de bottes finies et la pression physique commence deja a se sentir."
            : context.EnemyProfile.IsMostlyAp || context.EnemyProfile.HasHighPickThreat
                ? "Finir tes bottes maintenant te donne le tempo et la tenue qui manquent aux prochains moves."
                : "Terminer tes bottes te donne le spike de tempo le plus simple a convertir tout de suite.";

        recommendations.Add(BuildRecommendation(context, template, "Tempo", reason, 68));
    }

    private static ItemTemplate SelectAntiHealItem(ItemizationContext context)
    {
        return context.Archetype switch
        {
            BuildArchetype.AbilityPower => HasItem(context.PlayerItemIds, 3916)
                ? new ItemTemplate(3165, "Morellonomicon")
                : new ItemTemplate(3916, "Orbe d'oubli"),
            BuildArchetype.Tank or BuildArchetype.Support => HasItem(context.PlayerItemIds, 3076)
                ? new ItemTemplate(3193, "Epineuse")
                : new ItemTemplate(3076, "Cotte epineuse"),
            _ => HasItem(context.PlayerItemIds, 3123)
                ? new ItemTemplate(3033, "Rappel mortel")
                : new ItemTemplate(3123, "Appel de l'executeur")
        };
    }

    private static ItemTemplate SelectArmorItem(ItemizationContext context)
    {
        if (!HasCompletedBoots(context.Player, context.Catalog)
            && context.Role is PlayerRole.Adc or PlayerRole.Support
            && context.Threats.PhysicalPressure >= context.Threats.MagicPressure)
        {
            return new ItemTemplate(3047, "Bottes d'armure");
        }

        if (context.Archetype == BuildArchetype.AbilityPower || context.Role == PlayerRole.Mid)
        {
            return new ItemTemplate(3157, "Sablier de Zhonya");
        }

        if (context.Threats.CritPressure >= 2)
        {
            return new ItemTemplate(3143, "Presage de Randuin");
        }

        return context.Role is PlayerRole.Top or PlayerRole.Jungle or PlayerRole.Support
            ? new ItemTemplate(3110, "Coeur gele")
            : HasItem(context.PlayerItemIds, 3047)
                ? new ItemTemplate(3143, "Presage de Randuin")
                : new ItemTemplate(3047, "Bottes d'armure");
    }

    private static ItemTemplate SelectMagicResistItem(ItemizationContext context)
    {
        if (!HasCompletedBoots(context.Player, context.Catalog)
            && (context.EnemyProfile.HasHighPickThreat || context.Threats.MagicPressure >= context.Threats.PhysicalPressure))
        {
            return new ItemTemplate(3111, "Mercures");
        }

        return context.Archetype switch
        {
            BuildArchetype.AbilityPower => HasItem(context.PlayerItemIds, 3102)
                ? new ItemTemplate(3111, "Mercures")
                : new ItemTemplate(3102, "Voile de la banshee"),
            BuildArchetype.Tank or BuildArchetype.Support => HasItem(context.PlayerItemIds, 2504)
                ? new ItemTemplate(3105, "Force de la nature")
                : new ItemTemplate(2504, "Kaenic Rookern"),
            _ => HasItem(context.PlayerItemIds, 3155)
                ? new ItemTemplate(3156, "Maw of Malmortius")
                : new ItemTemplate(3155, "Hexdrinker")
        };
    }

    private static ItemTemplate SelectAntiTankItem(ItemizationContext context)
    {
        if (context.Archetype == BuildArchetype.AbilityPower)
        {
            return context.Threats.MagicResistStackCount >= 2
                ? new ItemTemplate(3135, "Baton du vide")
                : new ItemTemplate(6653, "Tourment de Liandry");
        }

        if (context.Role == PlayerRole.Adc)
        {
            return HasItem(context.PlayerItemIds, 3035)
                || context.CurrentGold >= 2600
                || context.GameState.GameTimeSeconds >= 1500
                ? new ItemTemplate(3036, "Salutations de Dominik")
                : new ItemTemplate(3035, "Dernier souffle");
        }

        return context.Role is PlayerRole.Top or PlayerRole.Jungle or PlayerRole.Support
            ? new ItemTemplate(3071, "Couperet noir")
            : new ItemTemplate(3035, "Dernier souffle");
    }

    private static ItemTemplate SelectSurvivalItem(ItemizationContext context)
    {
        if (CanRecommendQss(context))
        {
            return new ItemTemplate(3140, "QSS");
        }

        if (context.Archetype == BuildArchetype.Tank || IsFrontliner(context.Player, context.Catalog))
        {
            return !HasCompletedBoots(context.Player, context.Catalog)
                ? new ItemTemplate(3111, "Mercures")
                : HasItem(context.PlayerItemIds, 2504)
                    ? new ItemTemplate(3105, "Force de la nature")
                    : new ItemTemplate(2504, "Kaenic Rookern");
        }

        if (context.Archetype == BuildArchetype.AbilityPower || context.Role == PlayerRole.Mid)
        {
            return context.Threats.MagicPressure >= context.Threats.PhysicalPressure
                ? new ItemTemplate(3102, "Voile de la banshee")
                : new ItemTemplate(3157, "Sablier de Zhonya");
        }

        return context.EnemyProfile.HasHighPickThreat || context.Threats.MagicPressure >= context.Threats.PhysicalPressure
            ? new ItemTemplate(3102, "Voile de la banshee")
            : new ItemTemplate(3157, "Sablier de Zhonya");
    }

    private static ItemTemplate SelectBootsItem(ItemizationContext context)
    {
        if (context.Threats.PhysicalPressure >= context.Threats.MagicPressure + 2)
        {
            return new ItemTemplate(3047, "Bottes d'armure");
        }

        if (context.Threats.MagicPressure >= context.Threats.PhysicalPressure + 1 || context.EnemyProfile.HasHighPickThreat)
        {
            return new ItemTemplate(3111, "Mercures");
        }

        return context.Archetype switch
        {
            BuildArchetype.AbilityPower => new ItemTemplate(3020, "Bottes du sorcier"),
            BuildArchetype.Support => new ItemTemplate(3158, "Bottes de lucidite"),
            _ when context.Role == PlayerRole.Adc => new ItemTemplate(3006, "Berserker"),
            _ => new ItemTemplate(3158, "Bottes de lucidite")
        };
    }

    private static ItemRecommendation BuildRecommendation(
        ItemizationContext context,
        ItemTemplate template,
        string category,
        string reason,
        int priority)
    {
        var item = context.Catalog.Find(template.ItemId);
        return new ItemRecommendation
        {
            ItemId = template.ItemId,
            ItemName = item?.Name ?? template.FallbackName,
            IconUrl = item?.IconUrl,
            Category = category,
            Reason = reason,
            PurchaseHint = BuildPurchaseHint(context, item),
            Priority = priority
        };
    }

    private static string BuildPurchaseHint(ItemizationContext context, ItemCatalogEntry? item)
    {
        if (item is null)
        {
            return context.CurrentGold >= 1200
                ? "Bon timing de reset pour avancer dessus."
                : "Garde-le comme prochain slot prioritaire.";
        }

        var gold = (int)Math.Floor(context.CurrentGold);
        if (HoldableComponentItemIds.Contains(item.ItemId) && gold >= item.TotalGold)
        {
            return item.ItemId == 3140
                ? "Tu peux l'acheter comme bouton de cleanse sans forcer son upgrade tout de suite."
                : "Le composant seul a deja de la valeur, pas besoin de le completer tout de suite.";
        }

        if (item.TotalGold > 0 && gold >= item.TotalGold)
        {
            return "Achetable complet sur ton prochain reset.";
        }

        var ownedComponent = item.FromIds
            .Select(componentId => context.Catalog.Find(componentId))
            .FirstOrDefault(component => component is not null && context.PlayerItemIds.Contains(component.ItemId));
        if (ownedComponent is not null)
        {
            return $"Tu as deja {ownedComponent.Name} : complete-le au prochain reset.";
        }

        var affordableComponent = item.FromIds
            .Select(componentId => context.Catalog.Find(componentId))
            .Where(component => component is not null
                && !context.PlayerItemIds.Contains(component.ItemId)
                && component.TotalGold > 0
                && gold >= component.TotalGold)
            .OrderByDescending(component => component!.TotalGold)
            .FirstOrDefault();
        if (affordableComponent is not null)
        {
            return $"Reset sur {affordableComponent.Name} maintenant.";
        }

        return item.TotalGold > 0
            ? $"Il manque environ {Math.Max(0, item.TotalGold - gold)} gold pour le finir."
            : "Garde-le comme prochain slot prioritaire.";
    }

    private static string? BuildResistanceAdvice(ItemizationContext context)
    {
        if (context.EnemyProfile.IsMostlyAd || context.Threats.PhysicalPressure >= context.Threats.MagicPressure + 2)
        {
            return "La partie tire vers l'AD : armure, Steelcaps ou slot anti-crit deviennent tres rentables.";
        }

        if (context.EnemyProfile.IsMostlyAp || context.Threats.MagicPressure >= context.Threats.PhysicalPressure + 2)
        {
            return "La pression magique monte : vise RM, mercures ou une vraie reponse anti-burst.";
        }

        if (context.EnemyProfile.IsMixed)
        {
            return "Les degats restent mixtes : garde ton slot defensif flexible et n'hyper-stack pas une seule resistance.";
        }

        return null;
    }

    private static string? BuildSustainAdvice(ItemizationContext context)
    {
        if (context.Threats.SustainPressure < 2)
        {
            return null;
        }

        return context.AllyCoverage.AntiHealCount == 0
            ? "Le sustain ennemi n'est pas encore couvert par ton equipe : une source d'anti-heal a vite de la valeur."
            : "Le sustain ennemi reste notable : une deuxieme source d'anti-heal peut fiabiliser les fights.";
    }

    private static string? BuildDamagePatternAdvice(ItemizationContext context)
    {
        if (context.Threats.FrontlinePressure >= 2 || context.Threats.ArmorStackCount >= 2 || context.Threats.MagicResistStackCount >= 2)
        {
            return context.AllyCoverage.AntiTankCount == 0
                ? "Leur frontline se construit et ton equipe manque encore de shred. Pense penetration, burn ou anti-tank."
                : "Leur frontline rallonge deja les fights : ajoute de la penetration ou du DPS soutenu.";
        }

        return null;
    }

    private static string? BuildThreatAdvice(ItemizationContext context)
    {
        if (context.EnemyProfile.HasHighPickThreat)
        {
            return "La compo adverse joue le catch : garde un slot de survie et ne marche pas seul hors vision.";
        }

        if (context.EnemyProfile.HasHeavyEngage)
        {
            return "Leur engage est violent : prevois un vrai bouton defensif avant de greed un slot purement offensif.";
        }

        return null;
    }

    private static string FormatItemTip(ItemRecommendation recommendation)
    {
        return $"{recommendation.Category} : {recommendation.ItemName}. {recommendation.Reason} {recommendation.PurchaseHint}".Trim();
    }

    private static TeamCoverage BuildTeamCoverage(IEnumerable<PlayerSummary> players, ItemCatalogSnapshot catalog)
    {
        var roster = players.ToList();
        return new TeamCoverage(
            roster.Count(player => HasTrait(player, catalog, AntiHealItemIds, null)),
            roster.Count(player => HasTrait(player, catalog, AntiTankItemIds, null)),
            roster.Count(player => IsFrontliner(player, catalog)));
    }

    private static ThreatSignals BuildThreatSignals(
        GameState gameState,
        EnemyTeamProfile enemyProfile,
        ItemCatalogSnapshot catalog)
    {
        var enemies = gameState.Enemies;
        var physicalPressure = (enemyProfile.AdThreatCount * 2)
            + CountPlayersWithTag(enemies, catalog, "CriticalStrike")
            + CountPlayersWithTag(enemies, catalog, "AttackSpeed")
            + CountPlayersWithAny(enemies, CritItemIds);
        var magicPressure = (enemyProfile.ApThreatCount * 2)
            + CountPlayersWithTag(enemies, catalog, "SpellDamage")
            + CountPlayersWithAny(enemies, ApThreatItemIds);
        var sustainPressure = enemyProfile.SustainCount + CountPlayersWithAny(enemies, SustainItemIds);
        var frontlinePressure = enemyProfile.FrontlineCount
            + CountPlayersWithAtLeast(enemies, catalog, ArmorItemIds, "Armor", 1);
        var critPressure = CountPlayersWithAny(enemies, CritItemIds);
        var armorStackCount = CountPlayersWithAtLeast(enemies, catalog, ArmorItemIds, "Armor", 1);
        var magicResistStackCount = CountPlayersWithAtLeast(enemies, catalog, MagicResistItemIds, "SpellBlock", 1);

        return new ThreatSignals(
            physicalPressure,
            magicPressure,
            sustainPressure,
            frontlinePressure,
            enemyProfile.PickCount,
            enemyProfile.EngageCount,
            critPressure,
            armorStackCount,
            magicResistStackCount);
    }

    private static BuildArchetype InferArchetype(PlayerSummary player, PlayerRole role, ItemCatalogSnapshot catalog)
    {
        var championName = player.ChampionName ?? string.Empty;
        var apScore = CountTagMatches(player, catalog, "SpellDamage")
            + (ApChampions.Contains(championName) ? 2 : 0);
        var adScore = CountTagMatches(player, catalog, "Damage", "CriticalStrike", "AttackSpeed", "LifeSteal")
            + (AdChampions.Contains(championName) ? 2 : 0);
        var tankScore = CountTagMatches(player, catalog, "Armor", "SpellBlock", "Health")
            + (IsFrontliner(player, catalog) ? 2 : 0);

        if (role == PlayerRole.Adc)
        {
            return BuildArchetype.AttackDamage;
        }

        if (TankChampions.Contains(championName) && role is PlayerRole.Top or PlayerRole.Jungle or PlayerRole.Support)
        {
            return apScore >= 4
                ? BuildArchetype.AbilityPower
                : BuildArchetype.Tank;
        }

        if (role == PlayerRole.Mid && apScore >= adScore)
        {
            return BuildArchetype.AbilityPower;
        }

        if (apScore >= adScore + 2)
        {
            return BuildArchetype.AbilityPower;
        }

        if (adScore >= apScore + 1)
        {
            return BuildArchetype.AttackDamage;
        }

        if (tankScore >= Math.Max(apScore, adScore) + 2 && role is PlayerRole.Top or PlayerRole.Jungle or PlayerRole.Support)
        {
            return BuildArchetype.Tank;
        }

        return role switch
        {
            PlayerRole.Support => BuildArchetype.Support,
            PlayerRole.Mid => BuildArchetype.AbilityPower,
            PlayerRole.Top or PlayerRole.Jungle => BuildArchetype.AttackDamage,
            _ => BuildArchetype.AttackDamage
        };
    }

    private static bool CanRecommendQss(ItemizationContext context)
    {
        if (!context.EnemyProfile.HasHighPickThreat)
        {
            return false;
        }

        if (context.Role == PlayerRole.Adc)
        {
            return true;
        }

        return context.Archetype == BuildArchetype.AttackDamage
            && !IsFrontliner(context.Player, context.Catalog)
            && context.Role is PlayerRole.Top or PlayerRole.Jungle;
    }

    private static bool IsFrontliner(PlayerSummary player, ItemCatalogSnapshot catalog)
    {
        return FrontlineChampions.Contains(player.ChampionName ?? string.Empty)
            || CountTagMatches(player, catalog, "Armor", "SpellBlock", "Health") >= 2;
    }

    private static bool HasCompletedBoots(PlayerSummary player, ItemCatalogSnapshot catalog)
    {
        return player.Items.Any(item =>
            CompletedBootItemIds.Contains(item.ItemId)
            || (catalog.Find(item.ItemId) is { } itemData
                && itemData.Tags.Contains("Boots")
                && itemData.TotalGold >= 900));
    }

    private static bool HasTrait(
        PlayerSummary player,
        ItemCatalogSnapshot catalog,
        HashSet<int> traitItemIds,
        string? tag)
    {
        return CountTraitPieces(player, catalog, traitItemIds, tag) > 0;
    }

    private static int CountTraitPieces(
        PlayerSummary player,
        ItemCatalogSnapshot catalog,
        HashSet<int> traitItemIds,
        string? tag)
    {
        return player.Items.Count(item =>
            traitItemIds.Contains(item.ItemId)
            || (tag is not null && catalog.Find(item.ItemId)?.Tags.Contains(tag) == true));
    }

    private static int CountTagMatches(PlayerSummary player, ItemCatalogSnapshot catalog, params string[] tags)
    {
        return player.Items.Count(item =>
        {
            var itemData = catalog.Find(item.ItemId);
            return itemData is not null && tags.Any(tag => itemData.Tags.Contains(tag));
        });
    }

    private static int CountPlayersWithTag(IEnumerable<PlayerSummary> players, ItemCatalogSnapshot catalog, string tag)
    {
        return players.Count(player => player.Items.Any(item => catalog.Find(item.ItemId)?.Tags.Contains(tag) == true));
    }

    private static int CountPlayersWithAny(IEnumerable<PlayerSummary> players, HashSet<int> itemIds)
    {
        return players.Count(player => player.Items.Any(item => itemIds.Contains(item.ItemId)));
    }

    private static int CountPlayersWithAtLeast(
        IEnumerable<PlayerSummary> players,
        ItemCatalogSnapshot catalog,
        HashSet<int> itemIds,
        string? tag,
        int minMatches)
    {
        return players.Count(player => CountTraitPieces(player, catalog, itemIds, tag) >= minMatches);
    }

    private static bool HasAny(HashSet<int> playerItemIds, HashSet<int> targetIds)
    {
        return playerItemIds.Any(targetIds.Contains);
    }

    private static bool HasItem(HashSet<int> playerItemIds, int itemId)
    {
        return playerItemIds.Contains(itemId);
    }

    private static bool IsSamePlayer(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private enum BuildArchetype
    {
        AttackDamage,
        AbilityPower,
        Tank,
        Support
    }

    private sealed record ItemTemplate(int ItemId, string FallbackName);

    private sealed record TeamCoverage(int AntiHealCount, int AntiTankCount, int FrontlineCount);

    private sealed record ThreatSignals(
        int PhysicalPressure,
        int MagicPressure,
        int SustainPressure,
        int FrontlinePressure,
        int PickPressure,
        int EngagePressure,
        int CritPressure,
        int ArmorStackCount,
        int MagicResistStackCount);

    private sealed record ItemizationContext(
        GameState GameState,
        PlayerSummary Player,
        PlayerRole Role,
        BuildArchetype Archetype,
        ItemCatalogSnapshot Catalog,
        HashSet<int> PlayerItemIds,
        TeamCoverage AllyCoverage,
        TeamCoverage TeamCoverage,
        ThreatSignals Threats,
        EnemyTeamProfile EnemyProfile,
        double CurrentGold);
}
