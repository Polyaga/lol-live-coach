namespace LolLiveCoach.Desktop.Models;

public class ItemRecommendationDto
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string PurchaseHint { get; set; } = string.Empty;
    public int Priority { get; set; }

    public string ShortName
    {
        get
        {
            var parts = (ItemName ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(2)
                .Select(part => char.ToUpperInvariant(part[0]));

            var shortName = string.Concat(parts);
            return string.IsNullOrWhiteSpace(shortName) ? "IT" : shortName;
        }
    }
}
