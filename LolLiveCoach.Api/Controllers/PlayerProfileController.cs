using LolLiveCoach.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LolLiveCoach.Api.Controllers;

[ApiController]
[Route("api/player-profile")]
public class PlayerProfileController : ControllerBase
{
    private readonly RiotPlayerProfileService _riotPlayerProfileService;

    public PlayerProfileController(RiotPlayerProfileService riotPlayerProfileService)
    {
        _riotPlayerProfileService = riotPlayerProfileService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPlayerProfile(
        [FromQuery] string riotId,
        [FromQuery] string? platformRegion,
        CancellationToken cancellationToken)
    {
        var profile = await _riotPlayerProfileService.GetProfileAsync(riotId, platformRegion, cancellationToken);
        return Ok(profile);
    }
}
