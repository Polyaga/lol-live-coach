using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LolLiveCoach.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LolLiveCoach.Api.Services;

public class RiotPlayerProfileService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RiotPlayerProfileService> _logger;
    private readonly RiotApiOptions _options;

    public RiotPlayerProfileService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<RiotPlayerProfileService> logger,
        IOptions<RiotApiOptions> options)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _options = options.Value;

        _httpClient.Timeout = TimeSpan.FromSeconds(8);
        _httpClient.DefaultRequestHeaders.Remove("X-Riot-Token");

        var apiKey = _options.ApiKey?.Trim();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Riot-Token", apiKey);
        }
    }

    public async Task<PlayerProfileResponse> GetProfileAsync(
        string riotId,
        string? platformRegion,
        CancellationToken cancellationToken = default)
    {
        var normalizedApiKey = _options.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedApiKey))
        {
            return new PlayerProfileResponse
            {
                IsConfigured = false,
                IsAvailable = false,
                Message = "Le profil joueur demande une cle Riot API cote backend."
            };
        }

        var parsedRiotId = ParseRiotId(riotId);
        if (parsedRiotId is null)
        {
            return new PlayerProfileResponse
            {
                IsConfigured = true,
                IsAvailable = false,
                Message = "Renseigne un Riot ID au format NomDeJoueur#TAG."
            };
        }

        var normalizedPlatform = NormalizePlatform(platformRegion)
            ?? NormalizePlatform(_options.DefaultPlatformRegion);

        if (normalizedPlatform is null)
        {
            return new PlayerProfileResponse
            {
                IsConfigured = true,
                IsAvailable = false,
                Message = "La region de jeu est invalide. Exemple : EUW1."
            };
        }

        var cacheKey = $"riot-profile:{parsedRiotId.Normalized}:{normalizedPlatform}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(1, _options.CacheMinutes));
            return await LoadProfileAsync(parsedRiotId, normalizedPlatform, cancellationToken);
        }) ?? new PlayerProfileResponse
        {
            IsConfigured = true,
            IsAvailable = false,
            Message = "Le profil joueur est temporairement indisponible."
        };
    }

    private async Task<PlayerProfileResponse> LoadProfileAsync(
        ParsedRiotId riotId,
        string platformRegion,
        CancellationToken cancellationToken)
    {
        try
        {
            var routingRegion = ResolveRoutingRegion(platformRegion);
            if (routingRegion is null)
            {
                return new PlayerProfileResponse
                {
                    IsConfigured = true,
                    IsAvailable = false,
                    Message = $"La region {platformRegion} n'est pas prise en charge pour le moment."
                };
            }

            var accountUri = BuildUri(
                $"{routingRegion}.api.riotgames.com",
                $"/riot/account/v1/accounts/by-riot-id/{Uri.EscapeDataString(riotId.GameName)}/{Uri.EscapeDataString(riotId.TagLine)}");

            var accountResult = await GetJsonAsync<RiotAccountDto>(accountUri, cancellationToken);
            if (accountResult.StatusCode == HttpStatusCode.NotFound || accountResult.Value is null)
            {
                return new PlayerProfileResponse
                {
                    IsConfigured = true,
                    IsAvailable = false,
                    Message = $"Impossible de trouver {riotId.Normalized}. Verifie le Riot ID et la region."
                };
            }

            var account = accountResult.Value;
            var summonerUri = BuildUri(
                $"{platformRegion.ToLowerInvariant()}.api.riotgames.com",
                $"/lol/summoner/v4/summoners/by-puuid/{Uri.EscapeDataString(account.Puuid)}");
            var leagueUri = BuildUri(
                $"{platformRegion.ToLowerInvariant()}.api.riotgames.com",
                $"/lol/league/v4/entries/by-puuid/{Uri.EscapeDataString(account.Puuid)}");
            var recentMatchesCount = Math.Clamp(_options.RecentMatchesCount, 3, 8);
            var matchIdsUri = BuildUri(
                $"{routingRegion}.api.riotgames.com",
                $"/lol/match/v5/matches/by-puuid/{Uri.EscapeDataString(account.Puuid)}/ids?start=0&count={recentMatchesCount}");

            var summonerTask = GetJsonAsync<RiotSummonerDto>(summonerUri, cancellationToken);
            var leagueTask = GetJsonAsync<List<RiotLeagueEntryDto>>(leagueUri, cancellationToken);
            var matchIdsTask = GetJsonAsync<List<string>>(matchIdsUri, cancellationToken);

            await Task.WhenAll(summonerTask, leagueTask, matchIdsTask);

            var summoner = summonerTask.Result.Value;
            var leagueEntries = leagueTask.Result.Value ?? [];
            var matchIds = matchIdsTask.Result.Value ?? [];

            var recentMatches = await LoadRecentMatchesAsync(routingRegion, account.Puuid, matchIds, cancellationToken);
            var soloQueueEntry = leagueEntries.FirstOrDefault(entry =>
                string.Equals(entry.QueueType, "RANKED_SOLO_5x5", StringComparison.OrdinalIgnoreCase));

            return BuildProfileResponse(account, platformRegion, summoner, soloQueueEntry, recentMatches);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load Riot profile for {RiotId} on {PlatformRegion}", riotId.Normalized, platformRegion);
            return new PlayerProfileResponse
            {
                IsConfigured = true,
                IsAvailable = false,
                RiotId = riotId.Normalized,
                PlatformRegion = platformRegion,
                Message = ex.Message
            };
        }
    }

    private async Task<List<PlayerRecentMatch>> LoadRecentMatchesAsync(
        string routingRegion,
        string puuid,
        IEnumerable<string> matchIds,
        CancellationToken cancellationToken)
    {
        var tasks = matchIds.Select(async matchId =>
        {
            var matchUri = BuildUri(
                $"{routingRegion}.api.riotgames.com",
                $"/lol/match/v5/matches/{Uri.EscapeDataString(matchId)}");
            var result = await GetJsonAsync<RiotMatchDto>(matchUri, cancellationToken);
            var match = result.Value;
            var participant = match?.Info?.Participants?.FirstOrDefault(item =>
                string.Equals(item.Puuid, puuid, StringComparison.Ordinal));

            if (match?.Info is null || participant is null)
            {
                return null;
            }

            var totalCs = participant.TotalMinionsKilled + participant.NeutralMinionsKilled;
            var playedAt = match.Info.GameEndTimestamp > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(match.Info.GameEndTimestamp)
                : DateTimeOffset.FromUnixTimeMilliseconds(match.Info.GameCreation);

            return new PlayerRecentMatch
            {
                MatchId = match.Metadata?.MatchId ?? matchId,
                ChampionName = participant.ChampionName ?? "Champion inconnu",
                QueueLabel = MapQueueLabel(match.Info.QueueId),
                Win = participant.Win,
                Kills = participant.Kills,
                Deaths = participant.Deaths,
                Assists = participant.Assists,
                Cs = totalCs,
                DurationMinutes = (int)Math.Max(1, Math.Round(match.Info.GameDuration / 60d)),
                PlayedAt = playedAt
            };
        });

        var resolvedMatches = await Task.WhenAll(tasks);
        return resolvedMatches
            .Where(match => match is not null)
            .Cast<PlayerRecentMatch>()
            .OrderByDescending(match => match.PlayedAt)
            .ToList();
    }

    private PlayerProfileResponse BuildProfileResponse(
        RiotAccountDto account,
        string platformRegion,
        RiotSummonerDto? summoner,
        RiotLeagueEntryDto? soloQueueEntry,
        List<PlayerRecentMatch> recentMatches)
    {
        var totalKills = recentMatches.Sum(match => match.Kills);
        var totalDeaths = recentMatches.Sum(match => match.Deaths);
        var totalAssists = recentMatches.Sum(match => match.Assists);
        var totalWins = recentMatches.Count(match => match.Win);
        var totalMinutes = recentMatches.Sum(match => match.DurationMinutes);
        var totalCs = recentMatches.Sum(match => match.Cs);
        var sampleSize = recentMatches.Count;

        return new PlayerProfileResponse
        {
            IsConfigured = true,
            IsAvailable = true,
            Message = sampleSize == 0
                ? "Profil charge. Lance quelques parties pour remplir l'historique recent."
                : $"Profil mis a jour sur {sampleSize} parties recentes.",
            RiotId = $"{account.GameName}#{account.TagLine}",
            PlatformRegion = platformRegion,
            SummonerLevel = summoner?.SummonerLevel ?? 0,
            SoloQueueTier = FormatSoloQueue(soloQueueEntry),
            SoloQueueLeaguePoints = soloQueueEntry?.LeaguePoints ?? 0,
            SoloQueueWins = soloQueueEntry?.Wins ?? 0,
            SoloQueueLosses = soloQueueEntry?.Losses ?? 0,
            RecentKda = sampleSize == 0 ? 0 : Math.Round((totalKills + totalAssists) / Math.Max(1d, totalDeaths), 2),
            RecentWinRate = sampleSize == 0 ? 0 : Math.Round((double)totalWins / sampleSize * 100, 1),
            RecentCsPerMinute = totalMinutes == 0 ? 0 : Math.Round(totalCs / (double)totalMinutes, 1),
            RecentSampleSize = sampleSize,
            RecentMatches = recentMatches
        };
    }

    private async Task<RiotApiResult<T>> GetJsonAsync<T>(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new RiotApiResult<T>(default, response.StatusCode);
        }

        if (response.StatusCode == (HttpStatusCode)429)
        {
            throw new InvalidOperationException("Le profil joueur est temporairement limite par l'API Riot. Reessaie dans un instant.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Le profil joueur n'a pas pu etre charge ({(int)response.StatusCode}).");
        }

        var payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        return new RiotApiResult<T>(payload, response.StatusCode);
    }

    private static ParsedRiotId? ParseRiotId(string riotId)
    {
        var normalized = riotId?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var separatorIndex = normalized.LastIndexOf('#');
        if (separatorIndex <= 0 || separatorIndex >= normalized.Length - 1)
        {
            return null;
        }

        var gameName = normalized[..separatorIndex].Trim();
        var tagLine = normalized[(separatorIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(gameName) || string.IsNullOrWhiteSpace(tagLine))
        {
            return null;
        }

        return new ParsedRiotId(gameName, tagLine, $"{gameName}#{tagLine.ToUpperInvariant()}");
    }

    private static string? NormalizePlatform(string? platformRegion)
    {
        var normalized = platformRegion?.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? ResolveRoutingRegion(string platformRegion) => platformRegion.ToUpperInvariant() switch
    {
        "EUW1" or "EUN1" or "TR1" or "RU" => "europe",
        "NA1" or "BR1" or "LA1" or "LA2" => "americas",
        "KR" or "JP1" => "asia",
        "OC1" or "PH2" or "SG2" or "TH2" or "TW2" or "VN2" => "sea",
        _ => null
    };

    private static string FormatSoloQueue(RiotLeagueEntryDto? entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.Tier))
        {
            return "Non classe";
        }

        return $"{Capitalize(entry.Tier)} {entry.Rank}".Trim();
    }

    private static string MapQueueLabel(int queueId) => queueId switch
    {
        420 => "SoloQ",
        440 => "Flex",
        450 => "ARAM",
        400 => "Draft",
        430 => "Blind",
        490 => "Quickplay",
        700 => "Clash",
        _ => $"Queue {queueId}"
    };

    private static Uri BuildUri(string host, string path) => new($"https://{host}{path}", UriKind.Absolute);

    private static string Capitalize(string value)
    {
        var lower = value.ToLowerInvariant();
        return string.IsNullOrEmpty(lower)
            ? lower
            : char.ToUpperInvariant(lower[0]) + lower[1..];
    }

    private sealed record ParsedRiotId(string GameName, string TagLine, string Normalized);

    private sealed record RiotApiResult<T>(T? Value, HttpStatusCode StatusCode);

    private sealed class RiotAccountDto
    {
        [JsonPropertyName("puuid")]
        public string Puuid { get; set; } = string.Empty;

        [JsonPropertyName("gameName")]
        public string GameName { get; set; } = string.Empty;

        [JsonPropertyName("tagLine")]
        public string TagLine { get; set; } = string.Empty;
    }

    private sealed class RiotSummonerDto
    {
        [JsonPropertyName("summonerLevel")]
        public int SummonerLevel { get; set; }
    }

    private sealed class RiotLeagueEntryDto
    {
        [JsonPropertyName("queueType")]
        public string QueueType { get; set; } = string.Empty;

        [JsonPropertyName("tier")]
        public string Tier { get; set; } = string.Empty;

        [JsonPropertyName("rank")]
        public string Rank { get; set; } = string.Empty;

        [JsonPropertyName("leaguePoints")]
        public int LeaguePoints { get; set; }

        [JsonPropertyName("wins")]
        public int Wins { get; set; }

        [JsonPropertyName("losses")]
        public int Losses { get; set; }
    }

    private sealed class RiotMatchDto
    {
        [JsonPropertyName("metadata")]
        public RiotMatchMetadataDto? Metadata { get; set; }

        [JsonPropertyName("info")]
        public RiotMatchInfoDto? Info { get; set; }
    }

    private sealed class RiotMatchMetadataDto
    {
        [JsonPropertyName("matchId")]
        public string MatchId { get; set; } = string.Empty;
    }

    private sealed class RiotMatchInfoDto
    {
        [JsonPropertyName("gameCreation")]
        public long GameCreation { get; set; }

        [JsonPropertyName("gameEndTimestamp")]
        public long GameEndTimestamp { get; set; }

        [JsonPropertyName("gameDuration")]
        public long GameDuration { get; set; }

        [JsonPropertyName("queueId")]
        public int QueueId { get; set; }

        [JsonPropertyName("participants")]
        public List<RiotMatchParticipantDto> Participants { get; set; } = [];
    }

    private sealed class RiotMatchParticipantDto
    {
        [JsonPropertyName("puuid")]
        public string Puuid { get; set; } = string.Empty;

        [JsonPropertyName("championName")]
        public string? ChampionName { get; set; }

        [JsonPropertyName("kills")]
        public int Kills { get; set; }

        [JsonPropertyName("deaths")]
        public int Deaths { get; set; }

        [JsonPropertyName("assists")]
        public int Assists { get; set; }

        [JsonPropertyName("win")]
        public bool Win { get; set; }

        [JsonPropertyName("totalMinionsKilled")]
        public int TotalMinionsKilled { get; set; }

        [JsonPropertyName("neutralMinionsKilled")]
        public int NeutralMinionsKilled { get; set; }
    }
}
