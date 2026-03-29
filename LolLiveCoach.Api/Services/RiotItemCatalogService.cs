using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace LolLiveCoach.Api.Services;

public sealed class RiotItemCatalogService
{
    private const string CacheKey = "riot-item-catalog-fr-fr";
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<RiotItemCatalogService> _logger;

    public RiotItemCatalogService(
        HttpClient httpClient,
        IMemoryCache memoryCache,
        ILogger<RiotItemCatalogService> logger)
    {
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<ItemCatalogSnapshot> GetItemCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue<ItemCatalogSnapshot>(CacheKey, out var cachedCatalog)
            && cachedCatalog is not null)
        {
            return cachedCatalog;
        }

        try
        {
            var catalog = await FetchCatalogAsync(cancellationToken);
            _memoryCache.Set(CacheKey, catalog, TimeSpan.FromHours(6));
            return catalog;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load Riot item catalog. Item icons and prices will fallback.");
            _memoryCache.Set(CacheKey, ItemCatalogSnapshot.Empty, TimeSpan.FromMinutes(20));
            return ItemCatalogSnapshot.Empty;
        }
    }

    private async Task<ItemCatalogSnapshot> FetchCatalogAsync(CancellationToken cancellationToken)
    {
        var versions = await _httpClient.GetFromJsonAsync<List<string>>("api/versions.json", cancellationToken);
        var version = versions?.FirstOrDefault(version => !string.IsNullOrWhiteSpace(version));
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException("Riot version list is empty.");
        }

        var payload = await _httpClient.GetFromJsonAsync<DataDragonItemPayload>(
            $"cdn/{version}/data/fr_FR/item.json",
            cancellationToken);

        if (payload?.Data is null || payload.Data.Count == 0)
        {
            throw new InvalidOperationException("Riot item payload is empty.");
        }

        var items = new Dictionary<int, ItemCatalogEntry>();

        foreach (var pair in payload.Data)
        {
            if (!int.TryParse(pair.Key, out var itemId) || pair.Value is null)
            {
                continue;
            }

            var iconFile = string.IsNullOrWhiteSpace(pair.Value.Image?.Full)
                ? $"{itemId}.png"
                : pair.Value.Image.Full;

            items[itemId] = new ItemCatalogEntry(
                itemId,
                string.IsNullOrWhiteSpace(pair.Value.Name) ? $"Item {itemId}" : pair.Value.Name,
                pair.Value.Gold?.Total ?? 0,
                pair.Value.Gold?.Base ?? 0,
                ParseItemIds(pair.Value.From),
                new HashSet<string>(pair.Value.Tags ?? [], StringComparer.OrdinalIgnoreCase),
                $"https://ddragon.leagueoflegends.com/cdn/{version}/img/item/{iconFile}");
        }

        return new ItemCatalogSnapshot(version, items);
    }

    private static List<int> ParseItemIds(IEnumerable<string>? rawIds)
    {
        if (rawIds is null)
        {
            return [];
        }

        return rawIds
            .Select(id => int.TryParse(id, out var parsedId) ? parsedId : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private sealed class DataDragonItemPayload
    {
        [JsonPropertyName("data")]
        public Dictionary<string, DataDragonItemDefinition> Data { get; set; } = [];
    }

    private sealed class DataDragonItemDefinition
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("image")]
        public DataDragonImageDefinition? Image { get; set; }

        [JsonPropertyName("gold")]
        public DataDragonGoldDefinition? Gold { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("from")]
        public List<string>? From { get; set; }
    }

    private sealed class DataDragonImageDefinition
    {
        [JsonPropertyName("full")]
        public string? Full { get; set; }
    }

    private sealed class DataDragonGoldDefinition
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("base")]
        public int Base { get; set; }
    }
}

public sealed record ItemCatalogSnapshot(string Version, IReadOnlyDictionary<int, ItemCatalogEntry> Items)
{
    public static ItemCatalogSnapshot Empty { get; } =
        new(string.Empty, new Dictionary<int, ItemCatalogEntry>());

    public ItemCatalogEntry? Find(int itemId)
    {
        return Items.TryGetValue(itemId, out var item) ? item : null;
    }
}

public sealed record ItemCatalogEntry(
    int ItemId,
    string Name,
    int TotalGold,
    int BaseGold,
    IReadOnlyList<int> FromIds,
    HashSet<string> Tags,
    string? IconUrl);
