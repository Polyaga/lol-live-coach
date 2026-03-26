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
    RoleDetectorService roleDetectorService
    )
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
            
            // TODO: remplacer cette heuristique par une vraie correspondance avec activePlayer.summonerName
            var localPlayer = allPlayers.FirstOrDefault(p => !p.IsBot);

            var detectedRole = localPlayer is not null ? _roleDetectorService.DetectRole(localPlayer) : PlayerRole.Unknown;

            _logger.LogInformation("Active player detected: {SummonerName}", activePlayer?.SummonerName);

            foreach (var player in allPlayers)
            {
                _logger.LogInformation(
                    "Player: {SummonerName} | Team: {Team} | IsBot: {IsBot}",
                    player.SummonerName,
                    player.Team,
                    player.IsBot
                );
            }

            // Test brutal : en Practice Tool / custom, le seul joueur non-bot = joueur local
            var localPlayerTeam = allPlayers
                .Where(p => !p.IsBot)
                .Select(p => p.Team)
                .FirstOrDefault();

            _logger.LogInformation("Resolved local player team: {LocalPlayerTeam}", localPlayerTeam);

            var allies = allPlayers
                .Where(p => string.Equals(p.Team, localPlayerTeam, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var enemies = allPlayers
                .Where(p => !string.Equals(p.Team, localPlayerTeam, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return new GameState
            {
                IsInGame = true,
                GameMode = GetString(root, "gameData", "gameMode"),
                GameTimeSeconds = GetDouble(root, "gameData", "gameTime"),
                ActivePlayer = activePlayer,
                LocalPlayerTeam = localPlayerTeam,
                AllPlayers = allPlayers,
                Allies = allies,
                Enemies = enemies,
                Events = ParseEvents(root),
                DetectedRole = detectedRole
            };
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Unable to reach LoL Live Client API. Probably no game is running.");
            return new GameState { IsInGame = false };
        }
    }

    private static ActivePlayer? ParseActivePlayer(JsonElement root)
    {
        if (!root.TryGetProperty("activePlayer", out var activePlayerElement))
            return null;

        JsonElement championStats = default;
        if (activePlayerElement.TryGetProperty("championStats", out var stats))
            championStats = stats;

        return new ActivePlayer
        {
            SummonerName = GetString(root, "activePlayer", "summonerName"),
            Level = GetInt(activePlayerElement, "level"),
            CurrentHealth = championStats.ValueKind != JsonValueKind.Undefined ? GetDouble(championStats, "currentHealth") : 0,
            MaxHealth = championStats.ValueKind != JsonValueKind.Undefined ? GetDouble(championStats, "maxHealth") : 0,
            CurrentGold = GetDouble(root, "activePlayer", "currentGold"),
            IsDead = championStats.ValueKind != JsonValueKind.Undefined && GetDouble(championStats, "currentHealth") <= 0
        };
    }

    private static List<PlayerSummary> ParseAllPlayers(JsonElement root)
    {
        var players = new List<PlayerSummary>();

        if (!root.TryGetProperty("allPlayers", out var allPlayersElement) || allPlayersElement.ValueKind != JsonValueKind.Array)
            return players;

        foreach (var player in allPlayersElement.EnumerateArray())
        {
            players.Add(new PlayerSummary
            {
                SummonerName = GetString(player, "summonerName"),
                ChampionName = GetString(player, "championName"),
                Team = GetString(player, "team"),
                IsBot = GetBool(player, "isBot"),
                Level = GetInt(player, "level"),
                Kills = GetInt(player, "scores", "kills"),
                Deaths = GetInt(player, "scores", "deaths"),
                Assists = GetInt(player, "scores", "assists"),
                Items = ParseItems(player)
            });
        }

        return players;
    }

    private static List<ItemSummary> ParseItems(JsonElement player)
    {
        var items = new List<ItemSummary>();

        if (!player.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
            return items;

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
            return events;

        if (!eventsWrapper.TryGetProperty("Events", out var eventsElement) || eventsElement.ValueKind != JsonValueKind.Array)
            return events;

        foreach (var evt in eventsElement.EnumerateArray())
        {
            events.Add(new GameEvent
            {
                EventName = GetString(evt, "EventName"),
                EventTime = GetDouble(evt, "EventTime")
            });
        }

        return events;
    }

    private static string? GetString(JsonElement element, params string[] path)
    {
        var current = element;

        foreach (var key in path)
        {
            if (!current.TryGetProperty(key, out current))
                return null;
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }

    private static double GetDouble(JsonElement element, params string[] path)
    {
        var current = element;

        foreach (var key in path)
        {
            if (!current.TryGetProperty(key, out current))
                return 0;
        }

        return current.ValueKind == JsonValueKind.Number && current.TryGetDouble(out var value) ? value : 0;
    }

    private static int GetInt(JsonElement element, params string[] path)
    {
        var current = element;

        foreach (var key in path)
        {
            if (!current.TryGetProperty(key, out current))
                return 0;
        }

        return current.ValueKind == JsonValueKind.Number && current.TryGetInt32(out var value) ? value : 0;
    }

    private static bool GetBool(JsonElement element, params string[] path)
    {
        var current = element;

        foreach (var key in path)
        {
            if (!current.TryGetProperty(key, out current))
                return false;
        }

        return current.ValueKind == JsonValueKind.True
            || (current.ValueKind == JsonValueKind.False && current.GetBoolean());
    }
}