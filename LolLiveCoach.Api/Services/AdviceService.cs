using LolLiveCoach.Api.Models;

namespace LolLiveCoach.Api.Services;

public class AdviceService
{
    private const int PassivePriority = 0;
    private const int RoutinePriority = 1;
    private const int ImportantPriority = 2;
    private const int CriticalPriority = 3;
    private const double DragonSpawnSeconds = 300;
    private const double DragonRespawnSeconds = 300;
    private const double ElderRespawnSeconds = 360;
    private const double VoidgrubSpawnSeconds = 480;
    private const double HeraldSpawnSeconds = 900;
    private const double BaronSpawnSeconds = 1200;
    private const double BaronRespawnSeconds = 360;
    private const double ObjectivePrepWindowSeconds = 75;

    private readonly EnemyTeamAnalyzer _enemyTeamAnalyzer;
    private readonly BuildRecommendationService _buildRecommendationService;

    public AdviceService(
        EnemyTeamAnalyzer enemyTeamAnalyzer,
        BuildRecommendationService buildRecommendationService)
    {
        _enemyTeamAnalyzer = enemyTeamAnalyzer;
        _buildRecommendationService = buildRecommendationService;
    }

    public async Task<Advice> BuildAdviceAsync(GameState gameState, CancellationToken cancellationToken = default)
    {
        if (!gameState.IsInGame || gameState.ActivePlayer is null)
        {
            return new Advice
            {
                MainAdvice = "Aucune partie detectee.",
                Priority = PassivePriority
            };
        }

        var player = gameState.ActivePlayer;
        var localPlayer = gameState.LocalPlayer;
        var phase = GetGamePhase(gameState.GameTimeSeconds);
        var healthRatio = SafeRatio(player.CurrentHealth, player.MaxHealth, 1);
        var resourceProfile = BuildResourceProfile(player.ResourceType, player.MaxMana);
        var manaRatio = resourceProfile.DrivesMacroResourceAdvice
            ? SafeRatio(player.CurrentMana, player.MaxMana, 1)
            : 1;

        var enemyProfile = _enemyTeamAnalyzer.Analyze(gameState.Enemies);
        var buildAdvice = await _buildRecommendationService.BuildAsync(gameState, enemyProfile, cancellationToken);
        var objectiveWindow = localPlayer is null
            ? null
            : GetPriorityObjectiveWindow(gameState, localPlayer);
        var decision = SelectDecision(gameState, phase, localPlayer, objectiveWindow, resourceProfile, healthRatio, manaRatio);

        var advice = new Advice
        {
            MainAdvice = decision.MainAdvice,
            SecondaryAdvice = decision.SecondaryAdvice,
            GamePhase = phase,
            Priority = decision.Priority,
            BuildTips = buildAdvice.ItemTips
                .Where(tip => !string.IsNullOrWhiteSpace(tip))
                .Take(4)
                .ToList(),
            ItemRecommendations = buildAdvice.ItemRecommendations
                .OrderByDescending(recommendation => recommendation.Priority)
                .Take(3)
                .ToList()
        };

        AddObjectiveAlerts(advice, gameState, objectiveWindow);
        AddPerformanceAlerts(advice, gameState, localPlayer);
        AddBuildAlerts(advice, buildAdvice);

        return LimitAlerts(advice);
    }

    private static AdviceDecision SelectDecision(
        GameState gameState,
        string phase,
        PlayerSummary? localPlayer,
        ObjectiveWindow? objectiveWindow,
        ResourceProfile resourceProfile,
        double healthRatio,
        double manaRatio)
    {
        var activePlayer = gameState.ActivePlayer!;
        var gold = activePlayer.CurrentGold;

        if (activePlayer.IsDead)
        {
            return new AdviceDecision(
                "Profite du death timer.",
                BuildDeathTimerReason(localPlayer, phase),
                CriticalPriority);
        }

        var eventDecision = TryBuildRecentEventDecision(gameState, localPlayer, healthRatio, manaRatio, gold);
        if (eventDecision is not null)
        {
            return eventDecision;
        }

        var objectiveDecision = TryBuildObjectiveDecision(gameState, localPlayer, objectiveWindow, healthRatio, manaRatio, gold);
        if (objectiveDecision is not null)
        {
            return objectiveDecision;
        }

        if (healthRatio <= 0.28)
        {
            return new AdviceDecision(
                "Recall maintenant.",
                $"Tu n'as plus que {ToPercent(healthRatio)} PV. La prochaine prise de vision ou wave contestee peut te couter la game.",
                CriticalPriority);
        }

        if (resourceProfile.DrivesMacroResourceAdvice && manaRatio <= resourceProfile.LowResourceThreshold && gameState.GameTimeSeconds > 180)
        {
            return new AdviceDecision(
                "Reset sur ta prochaine fenetre.",
                $"Tu es a {ToPercent(manaRatio)} {resourceProfile.Label}. Sans ressources, ton prochain move sera souvent perdant meme si tes PV sont corrects.",
                ImportantPriority);
        }

        if (gold >= 1900)
        {
            return new AdviceDecision(
                "Depense ton or.",
                $"{Math.Round(gold)} gold non depenses. Ton prochain achat vaut plus qu'une minute de plus sur la map.",
                CriticalPriority);
        }

        if (gold >= 1200 && (healthRatio < 0.62 || manaRatio < 0.30))
        {
            return new AdviceDecision(
                "Prends le reset rentable.",
                $"{Math.Round(gold)} gold en poche avec des ressources entamees. Recall maintenant te rend plus fort sur le prochain timing.",
                ImportantPriority);
        }

        if (localPlayer is not null)
        {
            if (IsBehind(localPlayer, gameState))
            {
                return new AdviceDecision(
                    "Recupere une ressource sure.",
                    "Tu es sous pression. Une wave ou un camp propre vaut plus qu'un play force sans info.",
                    ImportantPriority);
            }

            if (IsAhead(localPlayer, healthRatio))
            {
                return new AdviceDecision(
                    "Appuie ton tempo fort.",
                    "Tu es en avance. Pousse, prends l'info profonde puis ressors avant la reponse ennemie.",
                    ImportantPriority);
            }
        }

        return BuildMacroDecision(gameState, phase);
    }

    private static AdviceDecision? TryBuildRecentEventDecision(
        GameState gameState,
        PlayerSummary? localPlayer,
        double healthRatio,
        double manaRatio,
        double gold)
    {
        if (localPlayer is null)
        {
            return null;
        }

        foreach (var gameEvent in gameState.Events
                     .Where(e => (gameState.GameTimeSeconds - e.EventTime) <= 22)
                     .OrderByDescending(e => e.EventTime))
        {
            var secondsAgo = Math.Max(0, gameState.GameTimeSeconds - gameEvent.EventTime);

            switch (gameEvent.EventName)
            {
                case "ChampionKill":
                    if (IsSamePlayer(gameEvent.VictimName, localPlayer.SummonerName))
                    {
                        return new AdviceDecision(
                            "Reviens sans tilt.",
                            $"Tu viens de mourir il y a {FormatSeconds(secondsAgo)}. Repars sur une wave ou un camp sur, pas sur un trade de revanche.",
                            CriticalPriority);
                    }

                    if (WasPlayerInvolved(gameEvent, localPlayer.SummonerName))
                    {
                        if (gold >= 1100 || healthRatio < 0.55 || manaRatio < 0.25)
                        {
                            return new AdviceDecision(
                                "Convertis puis reset.",
                                $"Kill recent il y a {FormatSeconds(secondsAgo)}. Prends la ressource gratuite la plus proche puis depense.",
                                CriticalPriority);
                        }

                        return new AdviceDecision(
                            "Convertis ce pick.",
                            $"Fenetre ouverte il y a {FormatSeconds(secondsAgo)}. Plaque, vision profonde ou objectif proche avant de reset.",
                            ImportantPriority);
                    }

                    if (IsAllyKill(gameEvent, gameState))
                    {
                        return new AdviceDecision(
                            "Joue la fenetre de surnombre.",
                            $"Un ennemi vient de tomber il y a {FormatSeconds(secondsAgo)}. Pousse la wave utile avant de bouger.",
                            ImportantPriority);
                    }

                    if (IsEnemyKillOnAlly(gameEvent, gameState))
                    {
                        return new AdviceDecision(
                            "Laisse passer le tempo ennemi.",
                            $"Un allie vient de mourir il y a {FormatSeconds(secondsAgo)}. Evite le facecheck et reprends une ressource sure.",
                            ImportantPriority);
                    }

                    break;

                case "DragonKill":
                case "BaronKill":
                case "HeraldKill":
                case "TurretKilled":
                case "InhibKilled":
                case "Ace":
                    var objectiveLabel = BuildObjectiveLabel(gameEvent);
                    if (IsAllyEvent(gameEvent, gameState))
                    {
                        if (gold >= 1000 || healthRatio < 0.60 || manaRatio < 0.30)
                        {
                            return new AdviceDecision(
                                "Objectif pris : reset.",
                                $"{objectiveLabel} securise. Depense et rejoue la map propre plutot que de greed un timing de trop.",
                                CriticalPriority);
                        }

                        return new AdviceDecision(
                            "Objectif pris : reorganise la map.",
                            $"{objectiveLabel} securise. Pousse les lanes proches puis pose la vision qui verrouille la suite.",
                            ImportantPriority);
                    }

                    if (IsEnemyEvent(gameEvent, gameState))
                    {
                        return new AdviceDecision(
                            "Objectif perdu : temporise.",
                            $"{objectiveLabel} concede. Ne check pas seul et prends le cross-map encore disponible.",
                            ImportantPriority);
                    }

                    break;

                case "Multikill":
                    if (IsSamePlayer(gameEvent.KillerName, localPlayer.SummonerName))
                    {
                        return new AdviceDecision(
                            "Protege ton shutdown.",
                            "Tu sors d'un gros play. Reset et rejoue avec tes achats plutot que de donner le retour ennemi.",
                            CriticalPriority);
                    }

                    break;
            }
        }

        return null;
    }

    private static AdviceDecision? TryBuildObjectiveDecision(
        GameState gameState,
        PlayerSummary? localPlayer,
        ObjectiveWindow? objectiveWindow,
        double healthRatio,
        double manaRatio,
        double gold)
    {
        if (localPlayer is null || objectiveWindow is null)
        {
            return null;
        }

        var enemyDeadCount = CountDeadPlayers(gameState.Enemies, 10);
        var allyDeadCount = CountDeadPlayers(
            gameState.Allies.Where(player => !IsSamePlayer(player.SummonerName, localPlayer.SummonerName)),
            10);
        var lowResources = healthRatio < 0.68 || manaRatio < 0.33 || gold >= 1100;

        if (objectiveWindow.TimeUntilSeconds > 0 && objectiveWindow.TimeUntilSeconds <= ObjectivePrepWindowSeconds)
        {
            if (lowResources)
            {
                return new AdviceDecision(
                    $"Reset avant {objectiveWindow.Name.ToLowerInvariant()}.",
                    $"{objectiveWindow.Name} dans {FormatCountdown(objectiveWindow.TimeUntilSeconds)}. Reviens avec achats, wards et tempo propre.",
                    CriticalPriority);
            }

            if (allyDeadCount >= 2 && enemyDeadCount == 0)
            {
                return new AdviceDecision(
                    $"Ne force pas {objectiveWindow.Name.ToLowerInvariant()}.",
                    $"{objectiveWindow.Name} arrive dans {FormatCountdown(objectiveWindow.TimeUntilSeconds)} mais vous etes en sous-nombre. Prepare plutot le cross-map.",
                    ImportantPriority);
            }

            return BuildRoleObjectivePrepDecision(gameState.DetectedRole, objectiveWindow);
        }

        if (objectiveWindow.IsAvailable)
        {
            if (enemyDeadCount >= 2 && allyDeadCount <= 1 && healthRatio >= 0.45 && manaRatio >= 0.18)
            {
                return BuildRoleObjectiveTakeDecision(gameState.DetectedRole, objectiveWindow, enemyDeadCount);
            }

            if (allyDeadCount >= 2 && enemyDeadCount == 0)
            {
                return new AdviceDecision(
                    $"Laisse {objectiveWindow.Name.ToLowerInvariant()}.",
                    $"{objectiveWindow.Name} est ouvert, mais vous etes en sous-nombre. Prends une wave, une tour ou la vision de sortie.",
                    ImportantPriority);
            }

            return BuildRoleObjectivePlayDecision(gameState.DetectedRole, objectiveWindow);
        }

        return null;
    }

    private static AdviceDecision BuildRoleObjectivePrepDecision(PlayerRole role, ObjectiveWindow objectiveWindow)
    {
        return role switch
        {
            PlayerRole.Jungle => new AdviceDecision(
                $"Path vers {objectiveWindow.Name.ToLowerInvariant()}.",
                $"{objectiveWindow.Name} dans {FormatCountdown(objectiveWindow.TimeUntilSeconds)}. Garde Smite, finis un camp court puis arrive le premier sur {BuildSideLabel(objectiveWindow.Side)}.",
                ImportantPriority),
            PlayerRole.Mid => new AdviceDecision(
                $"Push mid puis bouge {BuildSideShort(objectiveWindow.Side)}.",
                $"{objectiveWindow.Name} dans {FormatCountdown(objectiveWindow.TimeUntilSeconds)}. Ton push mid ouvre la riviere et empeche l'ennemi d'arriver gratuit.",
                ImportantPriority),
            PlayerRole.Support => new AdviceDecision(
                $"Reset wards pour {objectiveWindow.Name.ToLowerInvariant()}.",
                $"{objectiveWindow.Name} dans {FormatCountdown(objectiveWindow.TimeUntilSeconds)}. Reviens en premier pour verrouiller {BuildSideLabel(objectiveWindow.Side)}.",
                ImportantPriority),
            PlayerRole.Adc => new AdviceDecision(
                $"Prepare le timing {objectiveWindow.Name.ToLowerInvariant()}.",
                $"{objectiveWindow.Name} dans {FormatCountdown(objectiveWindow.TimeUntilSeconds)}. Prends une wave sure puis rejoins sans t'exposer seul.",
                ImportantPriority),
            PlayerRole.Top => new AdviceDecision(
                $"Raccourcis ta side avant {objectiveWindow.Name.ToLowerInvariant()}.",
                $"{objectiveWindow.Name} dans {FormatCountdown(objectiveWindow.TimeUntilSeconds)}. Fixe une wave courte puis garde-toi une fenetre de move.",
                ImportantPriority),
            _ => new AdviceDecision(
                $"Prepare {objectiveWindow.Name.ToLowerInvariant()}.",
                $"{objectiveWindow.Name} dans {FormatCountdown(objectiveWindow.TimeUntilSeconds)}. Reset, push et vision avant le timer.",
                ImportantPriority)
        };
    }

    private static AdviceDecision BuildRoleObjectiveTakeDecision(PlayerRole role, ObjectiveWindow objectiveWindow, int enemyDeadCount)
    {
        var baseMessage = objectiveWindow.Name switch
        {
            "Dragon" or "Elder" => $"Vous avez {enemyDeadCount} ennemis morts. {objectiveWindow.Name} est jouable maintenant.",
            "Nashor" => $"Vous avez {enemyDeadCount} ennemis morts. Nashor est une vraie conversion ici.",
            "Herald" => $"Vous avez {enemyDeadCount} ennemis morts. Herald te donne le meilleur tempo de carte.",
            _ => $"Vous avez {enemyDeadCount} ennemis morts. Convertis ce timing en objectif."
        };

        return role switch
        {
            PlayerRole.Jungle => new AdviceDecision(
                $"Lance {objectiveWindow.Name.ToLowerInvariant()}.",
                $"{baseMessage} Garde Smite et fais entrer ton equipe avant de commit profond.",
                CriticalPriority),
            PlayerRole.Support => new AdviceDecision(
                $"Couvre l'entree de {objectiveWindow.Name.ToLowerInvariant()}.",
                $"{baseMessage} Ta priorite est de fermer les entrees et proteger le carry pendant le setup.",
                CriticalPriority),
            PlayerRole.Adc => new AdviceDecision(
                $"Viens DPS {objectiveWindow.Name.ToLowerInvariant()}.",
                $"{baseMessage} N'ouvre pas la fog. Joue derriere la frontline et securise le plus de DPS possible.",
                CriticalPriority),
            _ => new AdviceDecision(
                $"Convertis sur {objectiveWindow.Name.ToLowerInvariant()}.",
                $"{baseMessage} Bouge maintenant avant que les respawns n'annulent la fenetre.",
                CriticalPriority)
        };
    }

    private static AdviceDecision BuildRoleObjectivePlayDecision(PlayerRole role, ObjectiveWindow objectiveWindow)
    {
        return role switch
        {
            PlayerRole.Jungle => new AdviceDecision(
                $"Joue autour de {objectiveWindow.Name.ToLowerInvariant()}.",
                $"{objectiveWindow.Name} est ouvert. Ton prochain camp doit te faire sortir sur {BuildSideLabel(objectiveWindow.Side)}, pas a l'oppose.",
                ImportantPriority),
            PlayerRole.Mid => new AdviceDecision(
                $"Garde la priorite mid pour {objectiveWindow.Name.ToLowerInvariant()}.",
                $"{objectiveWindow.Name} est ouvert. Si le mid reste pousse, ton equipe controle mieux la riviere.",
                ImportantPriority),
            PlayerRole.Support => new AdviceDecision(
                $"Prends la vision de {BuildSideLabel(objectiveWindow.Side)}.",
                $"{objectiveWindow.Name} est ouvert. Entre d'abord pour l'info, pas pour un engage sans couverture.",
                ImportantPriority),
            PlayerRole.Adc => new AdviceDecision(
                $"Reste connecte a {objectiveWindow.Name.ToLowerInvariant()}.",
                $"{objectiveWindow.Name} est disponible. Farm seulement la wave sure puis rejoue avec ton groupe.",
                ImportantPriority),
            PlayerRole.Top => new AdviceDecision(
                "Ne side pas trop loin.",
                $"{objectiveWindow.Name} est disponible. Une wave de trop peut te faire arriver trop tard sur le fight.",
                ImportantPriority),
            _ => new AdviceDecision(
                $"Respecte la zone de {objectiveWindow.Name.ToLowerInvariant()}.",
                $"{objectiveWindow.Name} est disponible. Vision et premier move valent plus qu'un reset de trop.",
                ImportantPriority)
        };
    }

    private static AdviceDecision BuildMacroDecision(GameState gameState, string phase)
    {
        var anchor = GetObjectiveAnchor(gameState.GameTimeSeconds);

        return phase switch
        {
            "Ouverture" => gameState.DetectedRole switch
            {
                PlayerRole.Top => new AdviceDecision(
                    "Garde ton HP pour le niveau 2.",
                    "Les deux premieres waves fixent souvent ton tempo de lane.",
                    RoutinePriority),
                PlayerRole.Jungle => new AdviceDecision(
                    "Pose un premier cycle propre.",
                    "Ton premier clear doit t'ouvrir une lane jouable, pas un gank force.",
                    RoutinePriority),
                PlayerRole.Mid => new AdviceDecision(
                    "Joue le push initial.",
                    "Tes deux premieres waves decident si tu peux aider riviere ou subir.",
                    RoutinePriority),
                PlayerRole.Adc => new AdviceDecision(
                    "Securise la premiere wave.",
                    "Conserve tes PV pour ne pas subir le premier reset botlane.",
                    RoutinePriority),
                PlayerRole.Support => new AdviceDecision(
                    "Prends l'info tres tot.",
                    "Le premier move utile est souvent un timing de vision, pas un all-in brut.",
                    RoutinePriority),
                _ => new AdviceDecision(
                    "Installe ton early proprement.",
                    "Les premiers resets et la prio de wave valent plus qu'un move improvise.",
                    RoutinePriority)
            },
            "Laning" => gameState.DetectedRole switch
            {
                PlayerRole.Top => new AdviceDecision(
                    "Travaille ta wave avant de bouger.",
                    "Si la wave n'est pas saine, ton move sera souvent plus cher qu'utile.",
                    RoutinePriority),
                PlayerRole.Jungle => new AdviceDecision(
                    "Finis ton cycle utile.",
                    "Joue sur la lane avec la priorite la plus simple, puis reviens sur tes camps.",
                    RoutinePriority),
                PlayerRole.Mid => new AdviceDecision(
                    "Pousse puis bouge.",
                    "Le mid push t'ouvre la riviere, les sides et les timings objectifs.",
                    RoutinePriority),
                PlayerRole.Adc => new AdviceDecision(
                    "Securise la prochaine wave.",
                    "Ton prochain spike d'item vaut plus qu'un trade flou sans couverture.",
                    RoutinePriority),
                PlayerRole.Support => new AdviceDecision(
                    "Prends l'info avant le play.",
                    "Riviere, reset et couverture du carry passent avant le coinflip.",
                    RoutinePriority),
                _ => new AdviceDecision(
                    "Joue ta ressource la plus propre.",
                    "Wave, camp ou reset : choisis ce qui te fait perdre le moins de tempo.",
                    RoutinePriority)
            },
            "Mid game" => gameState.DetectedRole switch
            {
                PlayerRole.Top => new AdviceDecision(
                    "Fixe une side puis rejoins.",
                    $"{anchor} Ne montre pas ta lane trop loin sans vraie info.",
                    RoutinePriority),
                PlayerRole.Jungle => new AdviceDecision(
                    "Joue du cote de l'objectif suivant.",
                    $"{anchor} Arriver avant le timer vaut souvent plus qu'un camp de plus.",
                    RoutinePriority),
                PlayerRole.Mid => new AdviceDecision(
                    "Garde le mid push.",
                    $"{anchor} Le mid push libere tes moves et empeche l'ennemi de sortir proprement.",
                    RoutinePriority),
                PlayerRole.Adc => new AdviceDecision(
                    "Reste sur la lane la plus sure.",
                    $"{anchor} Ton role est de DPS en vie, pas d'ouvrir les fogs.",
                    RoutinePriority),
                PlayerRole.Support => new AdviceDecision(
                    "Prepare la vision en avance.",
                    $"{anchor} Arrive avant tout le monde sur la zone cle.",
                    RoutinePriority),
                _ => new AdviceDecision(
                    "Recentre-toi sur le prochain timer.",
                    anchor,
                    RoutinePriority)
            },
            _ => gameState.DetectedRole switch
            {
                PlayerRole.Top => new AdviceDecision(
                    "Side courte puis regroupement.",
                    $"{anchor} En late, se faire attraper loin du groupe coute tres cher.",
                    RoutinePriority),
                PlayerRole.Jungle => new AdviceDecision(
                    "Controle vision et smite.",
                    $"{anchor} Ton positionnement avant le fight decide souvent plus que tes camps.",
                    RoutinePriority),
                PlayerRole.Mid => new AdviceDecision(
                    "Mid prioritaire avant tout.",
                    $"{anchor} Si le mid reste ferme, ton equipe peut respirer autour de Nashor.",
                    RoutinePriority),
                PlayerRole.Adc => new AdviceDecision(
                    "Reste colle au noyau.",
                    $"{anchor} Joue ton DPS avec couverture, pas en eclaireur.",
                    RoutinePriority),
                PlayerRole.Support => new AdviceDecision(
                    "Verrouille les entrees.",
                    $"{anchor} Ta vision et ton peel decident si l'equipe peut avancer.",
                    RoutinePriority),
                _ => new AdviceDecision(
                    "Joue groupement et vision.",
                    anchor,
                    RoutinePriority)
            }
        };
    }

    private static void AddPerformanceAlerts(Advice advice, GameState gameState, PlayerSummary? localPlayer)
    {
        if (localPlayer is null)
        {
            return;
        }

        if (localPlayer.Kills >= localPlayer.Deaths + 4)
        {
            advice.Alerts.Add("Tu as probablement une prime : ne donne pas ton shutdown sans vision ni soutien.");
        }

        if (localPlayer.Deaths >= localPlayer.Kills + 3)
        {
            advice.Alerts.Add("Tes morts pesent plus que tes kills : reduis le coinflip et priorise les ressources sures.");
        }

        AddLevelAlert(advice, gameState, localPlayer);
        AddFarmAlert(advice, gameState, localPlayer);
        AddVisionAlert(advice, gameState, localPlayer);
    }

    private static void AddLevelAlert(Advice advice, GameState gameState, PlayerSummary localPlayer)
    {
        if (gameState.Enemies.Count == 0)
        {
            return;
        }

        var highestEnemyLevel = gameState.Enemies.Max(enemy => enemy.Level);
        var levelGap = localPlayer.Level - highestEnemyLevel;

        if (levelGap <= -2)
        {
            advice.Alerts.Add("Retard de niveau notable : evite les trades frontaux jusqu'au prochain reset ou spike.");
        }
        else if (levelGap >= 2)
        {
            advice.Alerts.Add("Avance de niveau : vraie fenetre pour forcer si tes sorts clefs sont dispos.");
        }
    }

    private static void AddFarmAlert(Advice advice, GameState gameState, PlayerSummary localPlayer)
    {
        if (gameState.GameTimeSeconds < 480 || gameState.DetectedRole is PlayerRole.Support or PlayerRole.Unknown)
        {
            return;
        }

        var csPerMinute = localPlayer.CreepScore / Math.Max(1, gameState.GameTimeSeconds / 60d);
        var threshold = gameState.DetectedRole switch
        {
            PlayerRole.Jungle => 4.2,
            PlayerRole.Adc => 6.2,
            PlayerRole.Top => 5.8,
            PlayerRole.Mid => 5.8,
            _ => 0
        };

        if (threshold > 0 && csPerMinute < threshold)
        {
            advice.Alerts.Add($"Farm en dessous du rythme ({csPerMinute:0.0} cs/min) : reprends une wave ou un camp avant de forcer.");
        }
    }

    private static void AddVisionAlert(Advice advice, GameState gameState, PlayerSummary localPlayer)
    {
        var minutes = gameState.GameTimeSeconds / 60d;
        if (minutes < 8)
        {
            return;
        }

        var wardScoreThreshold = gameState.DetectedRole == PlayerRole.Support
            ? minutes * 1.1
            : minutes * 0.33;

        if (localPlayer.WardScore < wardScoreThreshold)
        {
            advice.Alerts.Add($"Vision legere pour {minutes:0} min : reprends la map avec un reset wards puis une entree propre.");
        }
    }

    private static void AddBuildAlerts(Advice advice, BuildAdviceProfile buildAdvice)
    {
        if (!string.IsNullOrWhiteSpace(buildAdvice.ResistanceAdvice))
        {
            advice.Alerts.Add(buildAdvice.ResistanceAdvice);
        }

        if (!string.IsNullOrWhiteSpace(buildAdvice.SustainAdvice))
        {
            advice.Alerts.Add(buildAdvice.SustainAdvice);
        }

        if (!string.IsNullOrWhiteSpace(buildAdvice.DamagePatternAdvice))
        {
            advice.Alerts.Add(buildAdvice.DamagePatternAdvice);
        }

        if (!string.IsNullOrWhiteSpace(buildAdvice.ThreatAdvice))
        {
            advice.Alerts.Add(buildAdvice.ThreatAdvice);
        }
    }

    private static void AddObjectiveAlerts(Advice advice, GameState gameState, ObjectiveWindow? objectiveWindow)
    {
        if (objectiveWindow is null)
        {
            return;
        }

        if (objectiveWindow.IsAvailable)
        {
            advice.Alerts.Add($"{objectiveWindow.Name} disponible sur {BuildSideLabel(objectiveWindow.Side)}.");
        }
        else if (objectiveWindow.TimeUntilSeconds <= ObjectivePrepWindowSeconds)
        {
            advice.Alerts.Add($"{objectiveWindow.Name} dans {FormatCountdown(objectiveWindow.TimeUntilSeconds)}.");
        }

        var enemyDeadCount = CountDeadPlayers(gameState.Enemies, 10);
        if (objectiveWindow.IsAvailable && enemyDeadCount >= 2)
        {
            advice.Alerts.Add($"{enemyDeadCount} ennemis morts : {objectiveWindow.Name.ToLowerInvariant()} jouable.");
        }
    }

    private static ObjectiveWindow? GetPriorityObjectiveWindow(GameState gameState, PlayerSummary localPlayer)
    {
        var candidates = GetObjectiveWindows(gameState)
            .Where(window => window.IsAvailable || window.TimeUntilSeconds <= ObjectivePrepWindowSeconds)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .OrderBy(window => window.IsAvailable ? 0 : 1)
            .ThenBy(window => GetObjectivePreferenceScore(window, localPlayer, gameState.GameTimeSeconds))
            .ThenBy(window => Math.Max(0, window.TimeUntilSeconds))
            .FirstOrDefault();
    }

    private static IEnumerable<ObjectiveWindow> GetObjectiveWindows(GameState gameState)
    {
        var dragonWindow = BuildDragonWindow(gameState);
        if (dragonWindow is not null)
        {
            yield return dragonWindow;
        }

        var voidgrubWindow = BuildVoidgrubWindow(gameState);
        if (voidgrubWindow is not null)
        {
            yield return voidgrubWindow;
        }

        var heraldWindow = BuildHeraldWindow(gameState);
        if (heraldWindow is not null)
        {
            yield return heraldWindow;
        }

        var baronWindow = BuildBaronWindow(gameState);
        if (baronWindow is not null)
        {
            yield return baronWindow;
        }
    }

    private static ObjectiveWindow? BuildDragonWindow(GameState gameState)
    {
        var dragonEvents = gameState.Events
            .Where(gameEvent => gameEvent.EventName == "DragonKill")
            .OrderBy(gameEvent => gameEvent.EventTime)
            .ToList();

        var allyDrakes = dragonEvents.Count(gameEvent => IsAllyEvent(gameEvent, gameState));
        var enemyDrakes = dragonEvents.Count(gameEvent => IsEnemyEvent(gameEvent, gameState));
        var elderCycle = allyDrakes >= 4 || enemyDrakes >= 4;
        var lastDragonTime = dragonEvents.LastOrDefault()?.EventTime;
        var nextSpawnTime = lastDragonTime is null
            ? DragonSpawnSeconds
            : lastDragonTime.Value + (elderCycle ? ElderRespawnSeconds : DragonRespawnSeconds);

        return new ObjectiveWindow(
            elderCycle ? "Elder" : "Dragon",
            "bot",
            nextSpawnTime - gameState.GameTimeSeconds,
            gameState.GameTimeSeconds >= nextSpawnTime);
    }

    private static ObjectiveWindow? BuildVoidgrubWindow(GameState gameState)
    {
        var grubKills = gameState.Events.Count(gameEvent =>
            gameEvent.EventName is "HordeKill" or "VoidgrubKill");
        if (grubKills >= 3 || gameState.GameTimeSeconds >= HeraldSpawnSeconds)
        {
            return null;
        }

        return new ObjectiveWindow(
            "Grubs",
            "top",
            VoidgrubSpawnSeconds - gameState.GameTimeSeconds,
            gameState.GameTimeSeconds >= VoidgrubSpawnSeconds);
    }

    private static ObjectiveWindow? BuildHeraldWindow(GameState gameState)
    {
        var heraldTaken = gameState.Events.Any(gameEvent => gameEvent.EventName == "HeraldKill");
        if (heraldTaken || gameState.GameTimeSeconds >= BaronSpawnSeconds)
        {
            return null;
        }

        return new ObjectiveWindow(
            "Herald",
            "top",
            HeraldSpawnSeconds - gameState.GameTimeSeconds,
            gameState.GameTimeSeconds >= HeraldSpawnSeconds);
    }

    private static ObjectiveWindow? BuildBaronWindow(GameState gameState)
    {
        var baronEvents = gameState.Events
            .Where(gameEvent => gameEvent.EventName == "BaronKill")
            .OrderBy(gameEvent => gameEvent.EventTime)
            .ToList();

        var nextSpawnTime = baronEvents.LastOrDefault() is { } lastBaron
            ? lastBaron.EventTime + BaronRespawnSeconds
            : BaronSpawnSeconds;

        if (gameState.GameTimeSeconds + ObjectivePrepWindowSeconds < BaronSpawnSeconds && baronEvents.Count == 0)
        {
            return null;
        }

        return new ObjectiveWindow(
            "Nashor",
            "top",
            nextSpawnTime - gameState.GameTimeSeconds,
            gameState.GameTimeSeconds >= nextSpawnTime);
    }

    private static int GetObjectivePreferenceScore(ObjectiveWindow window, PlayerSummary localPlayer, double gameTimeSeconds)
    {
        var role = InferRoleFromPosition(localPlayer.Position);

        return role switch
        {
            PlayerRole.Top => window.Name switch
            {
                "Herald" => 0,
                "Grubs" => 1,
                "Nashor" => gameTimeSeconds >= BaronSpawnSeconds ? 0 : 2,
                "Dragon" or "Elder" => 3,
                _ => 4
            },
            PlayerRole.Jungle => window.Name switch
            {
                "Nashor" => 0,
                "Dragon" or "Elder" => 1,
                "Herald" => 2,
                "Grubs" => 3,
                _ => 4
            },
            PlayerRole.Mid => window.Name switch
            {
                "Dragon" or "Elder" => 0,
                "Herald" => 1,
                "Nashor" => 1,
                "Grubs" => 2,
                _ => 4
            },
            PlayerRole.Adc => window.Name switch
            {
                "Dragon" or "Elder" => 0,
                "Nashor" => 1,
                "Herald" => 2,
                "Grubs" => 4,
                _ => 5
            },
            PlayerRole.Support => window.Name switch
            {
                "Dragon" or "Elder" => 0,
                "Nashor" => 1,
                "Herald" => 2,
                "Grubs" => 3,
                _ => 4
            },
            _ => window.Name switch
            {
                "Nashor" => 0,
                "Dragon" or "Elder" => 1,
                "Herald" => 2,
                "Grubs" => 3,
                _ => 4
            }
        };
    }

    private static bool IsBehind(PlayerSummary localPlayer, GameState gameState)
    {
        var enemyAverageLevel = gameState.Enemies.Count > 0
            ? gameState.Enemies.Average(enemy => enemy.Level)
            : localPlayer.Level;

        return localPlayer.Deaths >= localPlayer.Kills + 3
            || localPlayer.Level + 1 < enemyAverageLevel;
    }

    private static bool IsAhead(PlayerSummary localPlayer, double healthRatio)
    {
        return localPlayer.Kills >= localPlayer.Deaths + 3
            && healthRatio >= 0.55;
    }

    private static string BuildDeathTimerReason(PlayerSummary? localPlayer, string phase)
    {
        var respawnText = localPlayer?.RespawnTimer > 0
            ? $"Respawn dans {Math.Ceiling(localPlayer.RespawnTimer)}s."
            : "Utilise le timer pour ton achat et ton chemin de retour.";

        var phaseReminder = phase switch
        {
            "Laning" => "Reviens d'abord sur une ressource simple avant de vouloir rejouer un trade.",
            "Mid game" => "Pense au prochain objectif avant de sortir de base.",
            "Late game" => "En late, ton retour doit d'abord securiser la vision et le groupement.",
            _ => "Reviens sur un plan simple et stable."
        };

        return $"{respawnText} {phaseReminder}";
    }

    private static string GetObjectiveAnchor(double gameTimeSeconds)
    {
        if (gameTimeSeconds < VoidgrubSpawnSeconds)
        {
            return "Le prochain point chaud sera souvent autour du premier dragon ou des grubs.";
        }

        if (gameTimeSeconds < HeraldSpawnSeconds)
        {
            return "Dragon et Grubs sont les deux vraies fenetres a convertir dans les prochaines minutes.";
        }

        if (gameTimeSeconds < BaronSpawnSeconds)
        {
            return "Dragon, Herald et tours exterieures sont les meilleurs convertisseurs de tempo maintenant.";
        }

        return "Nashor, inhibiteurs et vision profonde valent plus qu'un pick improvise.";
    }

    private static string BuildObjectiveLabel(GameEvent gameEvent)
    {
        return gameEvent.EventName switch
        {
            "DragonKill" when string.Equals(gameEvent.DragonType, "Elder", StringComparison.OrdinalIgnoreCase) => "Elder",
            "DragonKill" when !string.IsNullOrWhiteSpace(gameEvent.DragonType) => $"Dragon {gameEvent.DragonType}",
            "DragonKill" => "Dragon",
            "BaronKill" => "Nashor",
            "HeraldKill" => "Herald",
            "TurretKilled" => "Tour",
            "InhibKilled" => "Inhibiteur",
            "Ace" => "Ace",
            _ => "Objectif"
        };
    }

    private static bool WasPlayerInvolved(GameEvent gameEvent, string? summonerName)
    {
        if (string.IsNullOrWhiteSpace(summonerName))
        {
            return false;
        }

        return IsSamePlayer(gameEvent.KillerName, summonerName)
            || gameEvent.Assisters.Any(assister => IsSamePlayer(assister, summonerName));
    }

    private static bool IsAllyKill(GameEvent gameEvent, GameState gameState)
    {
        return IsAllyName(gameEvent.KillerName, gameState) && IsEnemyName(gameEvent.VictimName, gameState);
    }

    private static bool IsEnemyKillOnAlly(GameEvent gameEvent, GameState gameState)
    {
        return IsEnemyName(gameEvent.KillerName, gameState) && IsAllyName(gameEvent.VictimName, gameState);
    }

    private static bool IsAllyEvent(GameEvent gameEvent, GameState gameState)
    {
        if (!string.IsNullOrWhiteSpace(gameEvent.AcingTeam) && !string.IsNullOrWhiteSpace(gameState.LocalPlayerTeam))
        {
            return string.Equals(gameEvent.AcingTeam, gameState.LocalPlayerTeam, StringComparison.OrdinalIgnoreCase);
        }

        return IsAllyName(gameEvent.KillerName, gameState) || IsAllyName(gameEvent.Acer, gameState);
    }

    private static bool IsEnemyEvent(GameEvent gameEvent, GameState gameState)
    {
        if (!string.IsNullOrWhiteSpace(gameEvent.AcingTeam) && !string.IsNullOrWhiteSpace(gameState.LocalPlayerTeam))
        {
            return !string.Equals(gameEvent.AcingTeam, gameState.LocalPlayerTeam, StringComparison.OrdinalIgnoreCase);
        }

        return IsEnemyName(gameEvent.KillerName, gameState) || IsEnemyName(gameEvent.Acer, gameState);
    }

    private static bool IsAllyName(string? summonerName, GameState gameState)
    {
        return gameState.Allies.Any(player => IsSamePlayer(player.SummonerName, summonerName));
    }

    private static bool IsEnemyName(string? summonerName, GameState gameState)
    {
        return gameState.Enemies.Any(player => IsSamePlayer(player.SummonerName, summonerName));
    }

    private static bool IsSamePlayer(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static PlayerRole InferRoleFromPosition(string? position)
    {
        return position?.ToUpperInvariant() switch
        {
            "TOP" => PlayerRole.Top,
            "JUNGLE" => PlayerRole.Jungle,
            "MIDDLE" => PlayerRole.Mid,
            "BOTTOM" => PlayerRole.Adc,
            "UTILITY" => PlayerRole.Support,
            _ => PlayerRole.Unknown
        };
    }

    private static string GetGamePhase(double gameTimeSeconds)
    {
        if (gameTimeSeconds < 90)
        {
            return "Ouverture";
        }

        if (gameTimeSeconds < 840)
        {
            return "Laning";
        }

        if (gameTimeSeconds < 1800)
        {
            return "Mid game";
        }

        return "Late game";
    }

    private static Advice LimitAlerts(Advice advice)
    {
        advice.Alerts = advice.Alerts
            .Where(alert => !string.IsNullOrWhiteSpace(alert))
            .Distinct()
            .Take(3)
            .ToList();

        return advice;
    }

    private static int CountDeadPlayers(IEnumerable<PlayerSummary> players, double minRespawnTimer)
    {
        return players.Count(player => player.IsDead && player.RespawnTimer >= minRespawnTimer);
    }

    private static string BuildSideLabel(string side)
    {
        return side == "bot" ? "la riviere bot" : "la riviere top";
    }

    private static string BuildSideShort(string side)
    {
        return side == "bot" ? "bot" : "top";
    }

    private static ResourceProfile BuildResourceProfile(string? resourceType, double maxResource)
    {
        if (string.Equals(resourceType, "MANA", StringComparison.OrdinalIgnoreCase))
        {
            return new ResourceProfile("mana", true, true, 0.16);
        }

        if (string.Equals(resourceType, "ENERGY", StringComparison.OrdinalIgnoreCase))
        {
            return new ResourceProfile("energie", false, true, 0.20);
        }

        if (!string.IsNullOrWhiteSpace(resourceType))
        {
            return new ResourceProfile(GetResourceLabel(resourceType), false, false, 0);
        }

        return maxResource > 0
            ? new ResourceProfile("mana", true, true, 0.16)
            : new ResourceProfile("ressource", false, false, 0);
    }

    private static string GetResourceLabel(string resourceType)
    {
        return resourceType.ToUpperInvariant() switch
        {
            "MANA" => "mana",
            "ENERGY" => "energie",
            "FURY" => "fureur",
            "RAGE" => "rage",
            "HEAT" => "chaleur",
            "SHIELD" => "bouclier",
            "FEROCITY" => "ferocite",
            "DRAGONFURY" => "fureur draconique",
            "BLOODWELL" => "puits de sang",
            _ => "ressource"
        };
    }

    private static double SafeRatio(double value, double maxValue, double fallback)
    {
        return maxValue > 0 ? value / maxValue : fallback;
    }

    private static string ToPercent(double value)
    {
        return $"{Math.Round(value * 100)}%";
    }

    private static string FormatSeconds(double secondsAgo)
    {
        return $"{Math.Max(1, (int)Math.Round(secondsAgo))}s";
    }

    private static string FormatCountdown(double seconds)
    {
        var totalSeconds = Math.Max(0, (int)Math.Ceiling(seconds));
        var minutes = totalSeconds / 60;
        var remainingSeconds = totalSeconds % 60;
        return $"{minutes}:{remainingSeconds:00}";
    }

    private sealed record AdviceDecision(string MainAdvice, string? SecondaryAdvice, int Priority);

    private sealed record ObjectiveWindow(string Name, string Side, double TimeUntilSeconds, bool IsAvailable);

    private sealed record ResourceProfile(
        string Label,
        bool DrivesMacroResourceAdvice,
        bool ShowOnOverlay,
        double LowResourceThreshold);
}
