namespace LolLiveCoach.Desktop.Models;

public class PlatformLoginResult
{
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
    public SubscriptionAccessDto? Access { get; set; }
    public PlatformUserDto? User { get; set; }
}

public class PlatformUserDto
{
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}
