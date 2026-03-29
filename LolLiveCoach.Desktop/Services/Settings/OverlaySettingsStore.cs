using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LolLiveCoach.Desktop.Models;

namespace LolLiveCoach.Desktop.Services;

public class OverlaySettingsStore
{
    private const string ProtectedPrefix = "dpapi:";
    private const string LegacyPlatformBaseUrl = "http://localhost:3000";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LolLiveCoach",
        "overlay-settings.json");

    public async Task<OverlaySettings> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            return OverlaySettings.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var storedSettings = await JsonSerializer.DeserializeAsync<OverlaySettings>(stream, SerializerOptions)
                ?? OverlaySettings.CreateDefault();
            return NormalizeForCurrentBuild(RestoreSensitiveValues(storedSettings));
        }
        catch
        {
            return OverlaySettings.CreateDefault();
        }
    }

    public async Task SaveAsync(OverlaySettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, PrepareForStorage(settings), SerializerOptions);
    }

    private static OverlaySettings PrepareForStorage(OverlaySettings settings)
    {
        return Clone(settings, Protect(settings.AccessKey));
    }

    private static OverlaySettings RestoreSensitiveValues(OverlaySettings settings)
    {
        return Clone(settings, Unprotect(settings.AccessKey));
    }

    private static OverlaySettings NormalizeForCurrentBuild(OverlaySettings settings)
    {
        var currentDefaultPlatformBaseUrl = OverlaySettings.DefaultPlatformBaseUrl;
        if (string.IsNullOrWhiteSpace(currentDefaultPlatformBaseUrl)
            || string.Equals(currentDefaultPlatformBaseUrl, LegacyPlatformBaseUrl, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(settings.PlatformBaseUrl, LegacyPlatformBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return settings;
        }

        var normalized = Clone(settings, settings.AccessKey);
        normalized.PlatformBaseUrl = currentDefaultPlatformBaseUrl;
        return normalized;
    }

    private static OverlaySettings Clone(OverlaySettings source, string accessKey)
    {
        return new OverlaySettings
        {
            ApiBaseUrl = source.ApiBaseUrl,
            PlatformBaseUrl = source.PlatformBaseUrl,
            AccountEmail = source.AccountEmail,
            AccessKey = accessKey,
            PreferredRiotId = source.PreferredRiotId,
            PreferredPlatformRegion = source.PreferredPlatformRegion,
            OverlayPosition = source.OverlayPosition,
            AutoStartLocalBackend = source.AutoStartLocalBackend,
            OverlayLeft = source.OverlayLeft,
            OverlayTop = source.OverlayTop,
            HistoryLeft = source.HistoryLeft,
            HistoryTop = source.HistoryTop,
            BuildLeft = source.BuildLeft,
            BuildTop = source.BuildTop
        };
    }

    private static string Protect(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (normalized.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            return normalized;
        }

        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(normalized),
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return $"{ProtectedPrefix}{Convert.ToBase64String(protectedBytes)}";
    }

    private static string Unprotect(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (!normalized.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            return normalized;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(normalized[ProtectedPrefix.Length..]);
            var clearBytes = ProtectedData.Unprotect(
                protectedBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(clearBytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}
