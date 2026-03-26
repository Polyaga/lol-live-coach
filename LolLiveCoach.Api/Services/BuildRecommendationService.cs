using LolLiveCoach.Api.Models;

namespace LolLiveCoach.Api.Services;

public class BuildRecommendationService
{
    public BuildAdviceProfile Build(GameState gameState, EnemyTeamProfile enemyProfile)
    {
        var result = new BuildAdviceProfile();

        if (enemyProfile.IsMostlyAd)
        {
            result.ResistanceAdvice = "Compo AD : privilégie l’armure.";
        }
        else if (enemyProfile.IsMostlyAp)
        {
            result.ResistanceAdvice = "Compo AP : privilégie la résistance magique.";
        }
        else if (enemyProfile.IsMixed)
        {
            result.ResistanceAdvice = "Compo mixte : évite de surinvestir trop tôt dans une seule résistance.";
        }

        if (enemyProfile.SustainCount >= 2)
        {
            result.SustainAdvice = "Sustain ennemi notable : anti-heal situationnel à envisager.";
        }

        return result;
    }
}