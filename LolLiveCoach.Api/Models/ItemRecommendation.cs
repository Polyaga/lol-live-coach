namespace LolLiveCoach.Api.Models;

public class ItemRecommendation
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string PurchaseHint { get; set; } = string.Empty;
    public int Priority { get; set; }
}
