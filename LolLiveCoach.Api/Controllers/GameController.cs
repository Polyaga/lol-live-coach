using LolLiveCoach.Api.Models;
using LolLiveCoach.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LolLiveCoach.Api.Controllers;

[ApiController]
[Route("api")]
public class GameController : ControllerBase
{
    private readonly LiveGameService _liveGameService;
    private readonly AdviceService _adviceService;
    private readonly EnemyTeamAnalyzer _enemyTeamAnalyzer;
    private readonly SubscriptionAccessService _subscriptionAccessService;

    public GameController(
        LiveGameService liveGameService,
        AdviceService adviceService,
        EnemyTeamAnalyzer enemyTeamAnalyzer,
        SubscriptionAccessService subscriptionAccessService)
    {
        _liveGameService = liveGameService;
        _adviceService = adviceService;
        _enemyTeamAnalyzer = enemyTeamAnalyzer;
        _subscriptionAccessService = subscriptionAccessService;
    }

    [HttpGet("game")]
    public async Task<IActionResult> GetGame(CancellationToken cancellationToken)
    {
        var state = await _liveGameService.GetGameStateAsync(cancellationToken);
        return Ok(state);
    }

    [HttpGet("advice")]
    public async Task<IActionResult> GetAdvice(CancellationToken cancellationToken)
    {
        var state = await _liveGameService.GetGameStateAsync(cancellationToken);
        var advice = await _adviceService.BuildAdviceAsync(state, cancellationToken);
        var access = await _subscriptionAccessService.GetCurrentAccessAsync(cancellationToken);

        return Ok(_subscriptionAccessService.ApplyAdviceEntitlements(advice, access));
    }

    [HttpGet("snapshot")]
    public async Task<IActionResult> GetSnapshot(CancellationToken cancellationToken)
    {
        var state = await _liveGameService.GetGameStateAsync(cancellationToken);
        var advice = await _adviceService.BuildAdviceAsync(state, cancellationToken);
        var access = await _subscriptionAccessService.GetCurrentAccessAsync(cancellationToken);

        return Ok(new CoachSnapshotResponse
        {
            Game = state,
            Advice = _subscriptionAccessService.ApplyAdviceEntitlements(advice, access),
            Access = access
        });
    }

    [HttpGet("enemy-profile")]
    public async Task<IActionResult> GetEnemyProfile(CancellationToken cancellationToken)
    {
        var state = await _liveGameService.GetGameStateAsync(cancellationToken);
        var profile = _enemyTeamAnalyzer.Analyze(state.Enemies);

        return Ok(new
        {
            state.LocalPlayerTeam,
            EnemyCount = state.Enemies.Count,
            Enemies = state.Enemies.Select(e => new
            {
                e.SummonerName,
                e.ChampionName,
                e.Team
            }),
            profile
        });
    }
}
