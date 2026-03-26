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

    public GameController(LiveGameService liveGameService, AdviceService adviceService, EnemyTeamAnalyzer enemyTeamAnalyzer)
    {
        _liveGameService = liveGameService;
        _adviceService = adviceService;
        _enemyTeamAnalyzer = enemyTeamAnalyzer;
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
        var advice = _adviceService.BuildAdvice(state);

        return Ok(advice);
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