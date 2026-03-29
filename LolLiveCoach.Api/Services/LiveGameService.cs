using System.Text.Json;
using LolLiveCoach.Api.Models;

namespace LolLiveCoach.Api.Services;

public class LiveGameService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LiveGameService> _logger;
    private readonly RoleDetectorService _roleDetectorService;

    public LiveGameService(
        HttpClient httpClient,
        ILogger<LiveGameService> logger,
        RoleDetectorService roleDetectorService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _roleDetectorService = roleDetectorService;
    }

    public async Task<GameState> GetGameStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/liveclientdata/allgamedata", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("LoL Live Client API returned status code {StatusCode}", response.StatusCode);
                return new GameState { IsInGame = false };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var activePlayer = ParseActivePlayer(root);
            var allPlayers = ParseAllPlayers(root);
            var localPlayer = ResolveLocalPlayer(activePlayer, allPlayers);
            var detectedRole = localPlayer is not null ? _roleDetectorService.DetectRole(localPlayer) : PlayerRole.Unknown;

            _logger.LogInformation("Active player detected: {SummonerName}", activePlayer?.SummonerName);

            foreach (var player in allPlayers)
            {
                _logger.LogInformation(
                    "Player: {SummonerName} | Team: {Team} | IsBot: {IsBot}",
                    player.SummonerName,
                    player.Team,
                    player.IsBot);
            }

            var localPlayerTeam = localPlayer?.Team
                ?? allPlayers
                    .Where(player => !player.IsBot)
                    .Select(player => player.Team)
                    .FirstOrDefault();

            _logger.LogInformation("Resolved local player team: {LocalPlayerTeam}", localPlayerTeam);

            var allies = allPlayers
                .Where(player => string.Equals(player.Team, localPlayerTeam, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var enemies = allPlayers
                .Where(player => !string.Equals(player.Team, localPlayerTeam, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return new GameState
            {
                IsInGame = true,
                GameMode = GetString(root, "gameData", "gameMode"),
                GameTimeSeconds = GetDouble(root, "gameData", "gameTime"),
                ActivePlayer = activePlayer,
                LocalPlayer = localPlayer,
                LocalPlayerTeam = localPlayerTeam,
                AllPlayers = allPlayers,
                Allies = allies,
                Enemies = enemies,
                Events = ParseEvents(root),
                DetectedRole = detectedRole
            };
        }
        catch (TaskCanceledException)
        {
            _logger.LogDebug("LoL Live Client API timed out. No live game data available.");
            return new GameState { IsInGame = false };
        }
        catch (HttpRequestException)
        {
            _logger.LogDebug("LoL Live Client API is unreachable. No game is currently exposed.");
            return new GameState { IsInGame = false };
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Unable to reach LoL Live Client API. Probably no game is running.");
            return new GameState { IsInGame = false };
        }
    }

    private static PlayerSummary? ResolveLocalPlayer(ActivePlayer? activePlayer, List<PlayerSummary> allPlayers)
    {
        if (!string.IsNullOrWhiteSpace(activePlayer?.SummonerName))
        {
            var exactMatch = allPlayers.FirstOrDefault(player =>
                string.Equals(player.SummonerName, activePlayer.SummonerName, StringComparison.OrdinalIgnoreCase));

            if (exactMatch is not null)
            {
                return exactMatch;
            }
        }

        var humanPlayers = allPlayers.Where(player => !player.IsBot).ToList();

        if (humanPlayers.Count == 1)
        {
            return humanPlayers[0];
        }

        return humanPlayers.FirstOrDefault();
    }

    private static ActivePlayer? ParseActivePlayer(JsonElement root)
    {
        if (!root.TryGetProperty("activePlayer", out var activePlayerElement))
        {
            return null;
        }

        JsonElement championStats = default;
        if (activePlayerElement.TryGetProperty("championStats", out var stats))
        {
            championStats = stats;
        }

        return new ActivePlayer
        {
            SummonerName = GetString(root, "activePlayer", "summonerName"),
            Level = GetInt(activePlayerElement, "level"),
            CurrentHealth = championStats.ValueKind != JsonValueKind.Undefined ? GetDouble(championStats, "currentHealth") : 0,
            MaxHealth = championStats.ValueKind != JsonValueKind.Undefined ? GetDouble(championStats, "maxHealth") : 0,
            ResourceType = championStats.ValueKind != JsonValueKind.Undefined ? GetString(championStats, "resourceType") : null,
            CurrentMana = championStats.ValueKind != JsonValueKind.Undefined ? GetDouble(championStats, "resourceValue") : 0,
            MaxMana = championStats.ValueKind != JsonValueKind.Undefined ? GetDouble(championStats, "resourceMax") : 0,
            CurrentGold = GetDouble(root, "activePlayer", "currentGold"),
            IsDead = championStats.ValueKind != JsonValueKind.Undefined && GetDouble(championStats, "currentHealth") <= 0
        };
    }

    private static List<PlayerSummary> ParseAllPlayers(JsonElement root)
    {
        var players = new List<PlayerSummary>();

        if (!root.TryGetProperty("allPlayers", out var allPlayersElement) || allPlayersElement.ValueKind != JsonValueKind.Array)
        {
            return players;
        }

        foreach (var player in allPlayersElement.EnumerateArray())
        {
            players.Add(new PlayerSummary
            {
                SummonerName = GetString(player, "summonerName"),
                ChampionName = GetString(player, "championName"),
                Team = GetString(player, "team"),
                Position = GetString(player, "position"),
                IsBot = GetBool(player, "isBot"),
                IsDead = GetBool(player, "isDead"),
                RespawnTimer = GetDouble(player, "respawnTimer"),
                Level = GetInt(player, "level"),
                Kills = GetInt(player, "scores", "kills"),
                Deaths = GetInt(player, "scores", "deaths"),
                Assists = GetInt(player, "scores", "assists"),
                CreepScore = GetInt(player, "scores", "creepScore"),
                WardScore = GetDouble(player, "scores", "wardScore"),
                Items = ParseItems(player)
            });
        }

        return players;
    }

    private static List<ItemSummary> ParseItems(JsonElement player)
    {
        var items = new List<ItemSummary>();

        if (!player.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        foreach (var item in itemsElement.EnumerateArray())
        {
            items.Add(new ItemSummary
            {
                ItemId = GetInt(item, "itemID"),
                DisplayName = GetString(item, "displayName"),
                Count = GetInt(item, "count")
            });
        }

        return items;
    }

    private static List<GameEvent> ParseEvents(JsonElement root)
    {
        var events = new List<GameEvent>();

        if (!root.TryGetProperty("events", out var eventsWrapper))
        {
            return events;
        }

        if (!eventsWrapper.TryGetProperty("Events", out var eventsElement) || eventsElement.ValueKind != JsonValueKind.Array)
        {
            return events;
        }

        foreach (var evt in eventsElement.EnumerateArray())
        {
            events.Add(new GameEvent
            {
                EventId = GetInt(evt, "EventID"),
                EventName = GetString(evt, "EventName"),
                EventTime = GetDouble(evt, "EventTime"),
                KillerName = GetString(evt, "KillerName"),
                VictimName = GetString(evt, "VictimName"),
                Assisters = GetStringArray(evt, "Assisters"),
                TurretKilled = GetString(evt, "TurretKilled"),
                InhibKilled = GetString(evt, "InhibKilled"),
                DragonType = GetString(evt, "DragonType"),
                Stolen = GetFlexibleBool(evt, "Stolen"),
                KillStreak = GetInt(evt, "KillStreak"),
                Acer = GetString(evt, "Acer"),
                AcingTeam = GetString(evt, "AcingTeam")
            });
        }

        return events;
    }

    private static List<string> GetStringArray(JsonElement element, params string[] path)
    {
        var current = element;

        foreach (var key in path)
        {
            if (!current.TryGetProperty(key, out current))
            {
                return [];
            }
        }

        if (current.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return current.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToList();
    }

    private static string? GetString(JsonElement element, params string[] path)
    {
        var current = element;

        foreach (var key in path)
        {
            if (!current.TryGetProperty(key, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }

    private static double GetDouble(JsonElement element, params string[] path)
    {
        var current = element;

        foreach (var key in path)
        {
            if (!current.TryGetProperty(key, out current))
            {
                return 0;
            }
        }

        return current.ValueKind == JsonValueKind.Number && current.TryGetDouble(out var value) ? value : 0;
    }

    private static int GetInt(JsonElement element, params string[] path)
    {
        var current = element;

        foreach (var key in path)
        {
            if (!current.TryGetProperty(key, out current))
            {
                return 0;
            }
        }

        return current.ValueKind == JsonValueKind.Number && current.TryGetInt32(out var value) ? value : 0;
    }

    private static bool GetBool(JsonElement element, params string[] path)
    {
        var current = element;

        foreach (var key in path)
        {
            if (!current.TryGetProperty(key, out current))
            {
                return false;
            }
        }

        return current.ValueKind == JsonValueKind.True
            || (current.ValueKind == JsonValueKind.False && current.GetBoolean());
    }

    private static bool GetFlexibleBool(JsonElement element, params string[] path)
    {
        var current = element;

        foreach (var key in path)
        {
            if (!current.TryGetProperty(key, out current))
            {
                return false;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(current.GetString(), out var value) && value,
            _ => false
        };
    }
}
