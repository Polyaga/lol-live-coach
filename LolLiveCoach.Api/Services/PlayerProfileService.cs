using LolLiveCoach.Api.Models;

namespace LolLiveCoach.Api.Services;

public class PlayerProfileService
{
    private readonly RemotePlayerProfileService _remotePlayerProfileService;
    private readonly RiotPlayerProfileService _riotPlayerProfileService;

    public PlayerProfileService(
        RemotePlayerProfileService remotePlayerProfileService,
        RiotPlayerProfileService riotPlayerProfileService)
    {
        _remotePlayerProfileService = remotePlayerProfileService;
        _riotPlayerProfileService = riotPlayerProfileService;
    }

    public async Task<PlayerProfileResponse> GetProfileAsync(
        string riotId,
        string? platformRegion,
        CancellationToken cancellationToken = default)
    {
        var localProfile = await _riotPlayerProfileService.GetProfileAsync(riotId, platformRegion, cancellationToken);

        if (localProfile.IsConfigured)
        {
            return localProfile;
        }

        return await _remotePlayerProfileService.GetProfileAsync(riotId, platformRegion, cancellationToken);
    }
}
