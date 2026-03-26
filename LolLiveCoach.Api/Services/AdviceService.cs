using LolLiveCoach.Api.Models;

namespace LolLiveCoach.Api.Services;

public class AdviceService
{
    private readonly EnemyTeamAnalyzer _enemyTeamAnalyzer;
    private readonly BuildRecommendationService _buildRecommendationService;

    public AdviceService(
        EnemyTeamAnalyzer enemyTeamAnalyzer,
        BuildRecommendationService buildRecommendationService)
    {
        _enemyTeamAnalyzer = enemyTeamAnalyzer;
        _buildRecommendationService = buildRecommendationService;
    }

    public Advice BuildAdvice(GameState gameState)
    {
        if (!gameState.IsInGame || gameState.ActivePlayer is null)
        {
            return new Advice
            {
                MainAdvice = "Aucune partie détectée."
            };
        }

        var advice = new Advice();

        var player = gameState.ActivePlayer;
        var healthRatio = player.MaxHealth > 0
            ? player.CurrentHealth / player.MaxHealth
            : 1;

        var gold = player.CurrentGold;
        var enemyProfile = _enemyTeamAnalyzer.Analyze(gameState.Enemies);
        var buildAdvice = _buildRecommendationService.Build(gameState, enemyProfile);

        if (player.IsDead)
        {
            advice.MainAdvice = "Tu es mort : profite du timer pour préparer ton achat.";
            AddBuildAlerts(advice, buildAdvice);
            AddRecentEventAlert(gameState, advice);
            return LimitAlerts(advice);
        }

        if (healthRatio < 0.25)
        {
            advice.MainAdvice = "Danger : très low HP, reset immédiat recommandé.";
            advice.Alerts.Add("Tu peux mourir sur ta prochaine erreur.");
        }
        else if (gold >= 1500)
        {
            advice.MainAdvice = "Recall fort recommandé : gros achat disponible.";
        }
        else if (gold >= 900 && healthRatio < 0.6)
        {
            advice.MainAdvice = "Recall intéressant : bon timing vu ton état actuel.";
        }
        else if (gameState.GameTimeSeconds < 90)
        {
            advice.MainAdvice = "Début de partie : rejoins ta lane.";
        }
        else
        {
            advice.MainAdvice = "Pas d’alerte majeure : continue à farm et prépare la prochaine fenêtre.";
        }

        AddBuildAlerts(advice, buildAdvice);
        AddRecentEventAlert(gameState, advice);

        return LimitAlerts(advice);
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
    }

    private static void AddRecentEventAlert(GameState gameState, Advice advice)
    {
        var recentKill = gameState.Events
            .Where(e => e.EventName == "ChampionKill")
            .OrderByDescending(e => e.EventTime)
            .FirstOrDefault();

        if (recentKill is not null && (gameState.GameTimeSeconds - recentKill.EventTime) <= 20)
        {
            advice.Alerts.Add("Un kill récent a eu lieu : regarde si un reset, un push ou un move est possible.");
        }
    }

    private static Advice LimitAlerts(Advice advice)
    {
        advice.Alerts = advice.Alerts
            .Distinct()
            .Take(3)
            .ToList();

        return advice;
    }
}