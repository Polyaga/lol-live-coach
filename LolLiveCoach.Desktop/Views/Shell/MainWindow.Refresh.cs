using System.Windows;
using System.Windows.Media;
using LolLiveCoach.Desktop.Models;

namespace LolLiveCoach.Desktop;

public partial class MainWindow
{
    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;

        try
        {
            var apiBaseUrl = GetApiBaseUrlFromUi();
            _apiClient.SetBaseAddress(apiBaseUrl);
            _apiClient.SetPlatformBaseUrl(GetPlatformBaseUrlFromUi());
            _apiClient.SetAccessKey(GetAccessKeyFromUi());

            var backendStatus = await _backendProcessManager.EnsureRunningAsync(
                apiBaseUrl,
                _apiClient,
                AutoStartBackendCheckBox.IsChecked == true);

            UpdateBackendStatus(backendStatus);

            if (!backendStatus.IsHealthy)
            {
                ShowDisconnectedState(backendStatus.Detail);
                return;
            }

            var snapshot = await _apiClient.GetSnapshotAsync();
            _lastSnapshot = snapshot;
            _currentAccess = snapshot.Access ?? SubscriptionAccessDto.CreateFree();
            _isInGame = snapshot.Game.IsInGame;

            UpdateAccessPanel(_currentAccess);
            UpdateMainWindow(snapshot);
            UpdateDisplayMode(snapshot.Game.IsInGame);
            ProcessNotifications(snapshot);
            HandleWindows(snapshot);
            await RefreshPlayerProfileIfNeededAsync();
        }
        catch (Exception ex)
        {
            ShowDisconnectedState(ex.Message);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void HandleWindows(CoachSnapshot snapshot)
    {
        if (snapshot.Game.IsInGame && snapshot.Access.CanUseInGameOverlay)
        {
            _overlayWindow.ApplySettings(_settings);
            _overlayWindow.UpdateSnapshot(snapshot);
            _overlayWindow.SetInteractive(false);

            if (!_overlayWindow.IsVisible)
            {
                _overlayWindow.Show();
            }

            if (IsVisible)
            {
                Hide();
            }

            return;
        }

        if (_overlayPreviewEnabled && !snapshot.Game.IsInGame)
        {
            _overlayWindow.ApplySettings(_settings);
            _overlayWindow.UpdateSnapshot(snapshot);
            _overlayWindow.SetInteractive(false);

            if (!_overlayWindow.IsVisible)
            {
                _overlayWindow.Show();
            }
        }
        else if (_overlayWindow.IsVisible)
        {
            _overlayWindow.Hide();
        }

        if (_notificationBubbleWindow.IsVisible)
        {
            _notificationBubbleWindow.Hide();
        }

        if (_historyOverlayWindow.IsVisible)
        {
            _historyOverlayWindow.SetInteractive(false);
            _historyOverlayWindow.Hide();
        }

        if (_buildOverlayWindow.IsVisible)
        {
            _buildOverlayWindow.SetInteractive(false);
            _buildOverlayWindow.Hide();
        }

        if (!IsVisible)
        {
            Show();
        }
    }

    private void UpdateMainWindow(CoachSnapshot snapshot)
    {
        var alerts = snapshot.Advice.Alerts ?? [];
        var itemRecommendations = snapshot.Advice.ItemRecommendations ?? [];
        var hasDetailedAdvice = snapshot.Access.CanSeeDetailedAdvice;
        var hasDetailedAlerts = snapshot.Access.CanSeeDetailedAlerts;

        LastUpdatedText.Text = $"Derniere mise a jour : {DateTime.Now:HH:mm:ss}";

        SessionStateText.Text = snapshot.Game.IsInGame ? "Partie detectee" : "Pret hors game";
        GamePhaseText.Text = $"Signal : {snapshot.Advice.GamePhase ?? "Preparation"}";
        MainAdviceText.Text = snapshot.Advice.MainAdvice;
        PremiumLockBanner.Visibility = snapshot.Access.HasPremiumAccess ? Visibility.Collapsed : Visibility.Visible;
        SecondaryAdviceText.Text = hasDetailedAdvice
            ? snapshot.Advice.SecondaryAdvice ?? "Le coach attend un contexte plus precis."
            : "Mode Free : passe au premium pour debloquer le conseil secondaire et les alertes detaillees.";

        AlertsItemsControl.ItemsSource = hasDetailedAlerts ? alerts.Take(3).ToList() : Array.Empty<string>();
        EmptyAlertsText.Text = hasDetailedAlerts
            ? "Aucune alerte critique pour le moment."
            : "Mode Free : les alertes detaillees, les bulles live et l'historique Tab sont reserves au premium.";
        EmptyAlertsText.Visibility = hasDetailedAlerts
            ? alerts.Count == 0 ? Visibility.Visible : Visibility.Collapsed
            : Visibility.Visible;

        BuildTipsItemsControl.ItemsSource = itemRecommendations.Take(3).ToList();
        EmptyBuildTipsText.Visibility = itemRecommendations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdatePreparationHub(snapshot);
        UpdatePatchReview(snapshot);
    }

    private void UpdateBackendStatus(BackendLaunchStatus status)
    {
        _isBackendHealthy = status.IsHealthy;
        _lastBackendDetail = BuildCoachStatusDetail(status);
        ConnectionStatusText.Text = BuildCoachStatusTitle(status);
        ConnectionStatusText.Foreground = status.IsHealthy
            ? new SolidColorBrush(Color.FromRgb(96, 211, 148))
            : new SolidColorBrush(Color.FromRgb(255, 107, 107));
        BackendDetailText.Text = _lastBackendDetail;
    }

    private void UpdateDisplayMode(bool isInGame)
    {
        DisplayModeText.Text = isInGame
            ? _currentAccess.CanUseInGameOverlay
                ? "Overlay live"
                : "Hub desktop"
            : _overlayPreviewEnabled
                ? "Preview + hub"
                : "Hub de preparation";
    }

    private void ShowDisconnectedState(string message)
    {
        _isInGame = false;

        if (_overlayWindow.IsVisible)
        {
            _overlayWindow.Hide();
        }

        if (_notificationBubbleWindow.IsVisible)
        {
            _notificationBubbleWindow.Hide();
        }

        if (_historyOverlayWindow.IsVisible)
        {
            _historyOverlayWindow.SetInteractive(false);
            _historyOverlayWindow.Hide();
        }

        if (_buildOverlayWindow.IsVisible)
        {
            _buildOverlayWindow.SetInteractive(false);
            _buildOverlayWindow.Hide();
        }

        if (!IsVisible)
        {
            Show();
        }

        UpdateBackendStatus(new BackendLaunchStatus
        {
            IsHealthy = false,
            IsOwnedByDesktop = false,
            Title = "Backend indisponible",
            Detail = message
        });
        LastUpdatedText.Text = "Derniere mise a jour : echec";
        SessionStateText.Text = "Connexion perdue";
        DisplayModeText.Text = "Hub hors ligne";
        GamePhaseText.Text = "Signal : hors ligne";
        MainAdviceText.Text = "Le desktop reste pret, mais le suivi live du coach n'est pas encore revenu.";
        PremiumLockBanner.Visibility = _currentAccess.HasPremiumAccess ? Visibility.Collapsed : Visibility.Visible;
        SecondaryAdviceText.Text = "Ouvre Parametres si tu veux relancer le suivi live ou verifier les reglages avances.";
        AlertsItemsControl.ItemsSource = Array.Empty<string>();
        EmptyAlertsText.Text = _currentAccess.CanSeeDetailedAlerts
            ? "Aucune alerte critique pour le moment."
            : "Mode Free : les alertes detaillees, les bulles live et l'historique Tab sont reserves au premium.";
        EmptyAlertsText.Visibility = Visibility.Visible;
        BuildTipsItemsControl.ItemsSource = Array.Empty<ItemRecommendationDto>();
        EmptyBuildTipsText.Visibility = Visibility.Visible;
        UpdatePreparationHub();
        UpdatePatchReview();
    }
}
