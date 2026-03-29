using System.Diagnostics;
using System.IO;
using LolLiveCoach.Desktop.Models;

namespace LolLiveCoach.Desktop.Services;

public sealed class BackendProcessManager : IDisposable
{
    private const string PackagedBackendDirectoryName = "backend";
    private const string BackendExecutableName = "LolLiveCoach.Api.exe";
    private const string BackendDllName = "LolLiveCoach.Api.dll";
    private Process? _ownedProcess;

    public async Task<BackendLaunchStatus> EnsureRunningAsync(
        string apiBaseUrl,
        CoachApiClient apiClient,
        bool allowAutoStart,
        CancellationToken cancellationToken = default)
    {
        if (await apiClient.IsHealthyAsync(cancellationToken))
        {
            return BuildHealthyStatus();
        }

        if (!ShouldLaunchLocally(apiBaseUrl))
        {
            return new BackendLaunchStatus
            {
                IsHealthy = false,
                IsOwnedByDesktop = false,
                Title = "Backend distant ou manuel",
                Detail = "L'app attend un backend deja disponible sur l'URL configuree."
            };
        }

        if (!allowAutoStart)
        {
            return new BackendLaunchStatus
            {
                IsHealthy = false,
                IsOwnedByDesktop = false,
                Title = "Demarrage automatique desactive",
                Detail = "Active l'option pour que l'app lance l'API locale toute seule."
            };
        }

        if (_ownedProcess is { HasExited: false })
        {
            return await WaitUntilHealthyAsync(apiClient, cancellationToken)
                ? BuildHealthyStatus("Backend local gere par l'app", "Le backend tourne deja dans cette session.")
                : BuildFailureStatus("Demarrage backend en attente", "Le backend local ne repond toujours pas.");
        }

        var startInfo = BuildPackagedStartInfo(apiBaseUrl) ?? BuildSourceStartInfo(apiBaseUrl);
        if (startInfo is null)
        {
            return BuildFailureStatus(
                "Backend introuvable",
                "Aucun backend publie ni projet source n'a ete trouve depuis l'app desktop.");
        }

        _ownedProcess = Process.Start(startInfo);

        return await WaitUntilHealthyAsync(apiClient, cancellationToken)
            ? BuildHealthyStatus(
                "Backend lance automatiquement",
                $"L'app a demarre l'API locale ({_ownedProcess?.Id}).")
            : BuildFailureStatus(
                "Echec du demarrage backend",
                "Le backend local a ete lance mais ne repond pas encore sur l'URL configuree.");
    }

    public async Task<BackendLaunchStatus> RestartAsync(
        string apiBaseUrl,
        CoachApiClient apiClient,
        CancellationToken cancellationToken = default)
    {
        StopOwnedProcess();
        return await EnsureRunningAsync(apiBaseUrl, apiClient, allowAutoStart: true, cancellationToken);
    }

    public void Dispose()
    {
        StopOwnedProcess();
    }

    private static bool ShouldLaunchLocally(string apiBaseUrl)
    {
        if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.IsLoopback
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static ProcessStartInfo? BuildPackagedStartInfo(string apiBaseUrl)
    {
        var packagedBackendDirectory = Path.Combine(AppContext.BaseDirectory, PackagedBackendDirectoryName);
        if (!Directory.Exists(packagedBackendDirectory))
        {
            return null;
        }

        var packagedExePath = Path.Combine(packagedBackendDirectory, BackendExecutableName);
        if (File.Exists(packagedExePath))
        {
            return CreateStartInfo(
                packagedExePath,
                arguments: null,
                packagedBackendDirectory,
                apiBaseUrl,
                environmentName: "Production");
        }

        var packagedDllPath = Path.Combine(packagedBackendDirectory, BackendDllName);
        return File.Exists(packagedDllPath)
            ? CreateStartInfo(
                "dotnet",
                $"\"{packagedDllPath}\"",
                packagedBackendDirectory,
                apiBaseUrl,
                environmentName: "Production")
            : null;
    }

    private static ProcessStartInfo? BuildSourceStartInfo(string apiBaseUrl)
    {
        var apiProjectPath = FindApiProjectPath();
        if (apiProjectPath is null)
        {
            return null;
        }

        var projectDirectory = Path.GetDirectoryName(apiProjectPath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return null;
        }

        return CreateStartInfo(
            "dotnet",
            $"run --no-launch-profile --project \"{apiProjectPath}\"",
            projectDirectory,
            apiBaseUrl,
            environmentName: "Development");
    }

    private static string? FindApiProjectPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "LolLiveCoach.Api", "LolLiveCoach.Api.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private static ProcessStartInfo CreateStartInfo(
        string fileName,
        string? arguments,
        string workingDirectory,
        string apiBaseUrl,
        string environmentName)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            startInfo.Arguments = arguments;
        }

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = environmentName;
        startInfo.Environment["DOTNET_ENVIRONMENT"] = environmentName;
        startInfo.Environment["ASPNETCORE_URLS"] = BuildLoopbackBindingUrl(apiBaseUrl);
        return startInfo;
    }

    private static string BuildLoopbackBindingUrl(string apiBaseUrl)
    {
        return Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Authority)
            : OverlaySettings.DefaultApiBaseUrl;
    }

    private static BackendLaunchStatus BuildFailureStatus(string title, string detail) => new()
    {
        IsHealthy = false,
        IsOwnedByDesktop = false,
        Title = title,
        Detail = detail
    };

    private BackendLaunchStatus BuildHealthyStatus(
        string title = "Backend connecte",
        string detail = "Le backend est joignable.")
        => new()
        {
            IsHealthy = true,
            IsOwnedByDesktop = _ownedProcess is { HasExited: false },
            Title = title,
            Detail = detail
        };

    private void StopOwnedProcess()
    {
        try
        {
            if (_ownedProcess is { HasExited: false })
            {
                _ownedProcess.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
        finally
        {
            _ownedProcess = null;
        }
    }

    private static async Task<bool> WaitUntilHealthyAsync(CoachApiClient apiClient, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (await apiClient.IsHealthyAsync(cancellationToken))
            {
                return true;
            }

            await Task.Delay(500, cancellationToken);
        }

        return false;
    }
}
