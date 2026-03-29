using System.Reflection;

namespace LolLiveCoach.Desktop.Services;

internal static class AppBuildMetadata
{
    private static readonly IReadOnlyDictionary<string, string> Metadata = Assembly
        .GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Key))
        .GroupBy(attribute => attribute.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            group => group.Key,
            group => group.Last().Value ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);

    public static string? GetValue(string key)
    {
        return Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }
}
