using System.Windows;
using Velopack;
using Velopack.Sources;

namespace LolLiveCoach.Desktop.Services;

public sealed class AppUpdateService
{
    private readonly string? _updateFeedUrl = AppBuildMetadata.GetValue("UpdateFeedUrl");
    private readonly string? _updateChannel = AppBuildMetadata.GetValue("UpdateChannel");
    private int _hasCheckedForUpdates;
    private bool _isApplyingUpdate;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_updateFeedUrl);

    public async Task CheckForUpdatesInBackgroundAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || Interlocked.Exchange(ref _hasCheckedForUpdates, 1) == 1)
        {
            return;
        }

        try
        {
            var manager = CreateUpdateManager();
            if (!manager.IsInstalled)
            {
                return;
            }

            if (manager.UpdatePendingRestart is { } pendingUpdate)
            {
                await PromptForRestartAsync(manager, pendingUpdate);
                return;
            }

            var availableUpdate = await manager.CheckForUpdatesAsync();
            if (availableUpdate is null)
            {
                return;
            }

            await manager.DownloadUpdatesAsync(availableUpdate, progress: null, cancelToken: cancellationToken);
            await PromptForRestartAsync(manager, availableUpdate.TargetFullRelease);
        }
        catch
        {
            // Silent by design: release builds should not block the UX if the feed is unreachable.
        }
    }

    private UpdateManager CreateUpdateManager()
    {
        var feedUrl = _updateFeedUrl?.Trim() ?? string.Empty;

        if (feedUrl.Contains("github.com/", StringComparison.OrdinalIgnoreCase))
        {
            var includePrerelease = !string.Equals(_updateChannel, "stable", StringComparison.OrdinalIgnoreCase);
            return string.IsNullOrWhiteSpace(_updateChannel)
                ? new UpdateManager(new GithubSource(feedUrl, accessToken: null, prerelease: includePrerelease))
                : new UpdateManager(new GithubSource(feedUrl, accessToken: null, prerelease: includePrerelease), new UpdateOptions
                {
                    ExplicitChannel = _updateChannel
                });
        }

        return string.IsNullOrWhiteSpace(_updateChannel)
            ? new UpdateManager(feedUrl)
            : new UpdateManager(feedUrl, new UpdateOptions
            {
                ExplicitChannel = _updateChannel
            });
    }

    private async Task PromptForRestartAsync(UpdateManager manager, VelopackAsset update)
    {
        if (Application.Current is null || _isApplyingUpdate)
        {
            return;
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_isApplyingUpdate)
            {
                return;
            }

            var result = MessageBox.Show(
                $"La version {update.Version} est prete. Redemarrer maintenant pour l'installer ?",
                "Mise a jour prete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            _isApplyingUpdate = true;
            manager.WaitExitThenApplyUpdates(update, silent: false, restart: true, restartArgs: Array.Empty<string>());
            Application.Current.Shutdown();
        });
    }
}
