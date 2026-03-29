using LolLiveCoach.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LolLiveCoach.Api.Controllers;

[ApiController]
[Route("api/player-profile")]
public class PlayerProfileController : ControllerBase
{
    private readonly PlayerProfileService _playerProfileService;

    public PlayerProfileController(PlayerProfileService playerProfileService)
    {
        _playerProfileService = playerProfileService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPlayerProfile(
        [FromQuery] string riotId,
        [FromQuery] string? platformRegion,
        CancellationToken cancellationToken)
    {
        var profile = await _playerProfileService.GetProfileAsync(riotId, platformRegion, cancellationToken);
        return Ok(profile);
    }
}
