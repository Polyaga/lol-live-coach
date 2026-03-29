using System.Windows;
using System.Windows.Controls;
using LolLiveCoach.Desktop.Models;

namespace LolLiveCoach.Desktop;

public partial class MainWindow
{
    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _settings = BuildSettingsFromUi(_settings.AccessKey);

        _overlayWindow.ApplySettings(_settings);
        _notificationBubbleWindow.ApplySettings(_settings);
        _historyOverlayWindow.ApplySettings(_settings);
        _buildOverlayWindow.ApplySettings(_settings);
        await _settingsStore.SaveAsync(_settings);
        await RefreshAsync();
    }

    private async void RestartBackendButton_Click(object sender, RoutedEventArgs e)
    {
        _apiClient.SetBaseAddress(GetApiBaseUrlFromUi());
        _apiClient.SetPlatformBaseUrl(GetPlatformBaseUrlFromUi());
        _apiClient.SetAccessKey(GetAccessKeyFromUi());
        UpdateBackendStatus(new BackendLaunchStatus
        {
            IsHealthy = false,
            IsOwnedByDesktop = true,
            Title = "Redemarrage backend...",
            Detail = "L'app relance le backend local."
        });

        var status = await _backendProcessManager.RestartAsync(GetApiBaseUrlFromUi(), _apiClient);
        UpdateBackendStatus(status);
        await RefreshAsync();
    }

    private void ToggleOverlayPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_currentAccess.CanUseOverlayPreview)
        {
            MessageBox.Show(
                "La previsualisation de l'overlay n'est pas disponible avec la configuration actuelle.",
                "Overlay indisponible",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (_isInGame)
        {
            MessageBox.Show(
                "La preview de l'overlay est reservee aux moments hors partie.",
                "Preview indisponible",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _overlayPreviewEnabled = !_overlayPreviewEnabled;
        UpdateOverlayPreviewButtonText();

        if (_overlayPreviewEnabled)
        {
            var previewSnapshot = _lastSnapshot ?? new CoachSnapshot
            {
                Advice = new AdviceDto
                {
                    MainAdvice = "Preview overlay active.",
                    SecondaryAdvice = "Regle la position pendant que tu es hors partie.",
                    GamePhase = "Preview",
                    Alerts = ["Le coach desktop est pret.", "Choisis le coin le plus confortable.", "L'overlay restera non bloquant en game."],
                    BuildTips = ["Ici apparaitront les axes de stuff clefs du draft.", "Maintiens Tab en game pour afficher et replacer les panneaux."],
                    ItemRecommendations =
                    [
                        new ItemRecommendationDto
                        {
                            ItemName = "Priorite anti-heal",
                            Category = "Exemple",
                            Reason = "Le coach te montrera ici un item contextualise selon le sustain adverse.",
                            PurchaseHint = "Le nom, l'icone et le timing d'achat apparaitront en live."
                        },
                        new ItemRecommendationDto
                        {
                            ItemName = "Reponse defensive",
                            Category = "Exemple",
                            Reason = "Le panneau compare deja ton stuff, celui des allies et des ennemis.",
                            PurchaseHint = "Maintiens Tab en game pour voir et replacer ces cartes."
                        }
                    ]
                },
                Game = new GameStateDto
                {
                    IsInGame = false,
                    DetectedRole = 0,
                    LocalPlayer = new PlayerSummaryDto
                    {
                        ChampionName = "Coach",
                        Kills = 0,
                        Deaths = 0,
                        Assists = 0
                    }
                },
                Access = _currentAccess
            };

            _overlayWindow.ApplySettings(_settings);
            _overlayWindow.UpdateSnapshot(previewSnapshot);
            _overlayWindow.Show();
        }
        else if (_overlayWindow.IsVisible)
        {
            _overlayWindow.Hide();
        }

        UpdateDisplayMode(false);
        UpdatePreparationHub(_lastSnapshot);
    }

    private void ApplySettingsToUi()
    {
        ApiBaseUrlTextBox.Text = _settings.ApiBaseUrl;
        PlatformBaseUrlTextBox.Text = _settings.PlatformBaseUrl;
        AccountEmailTextBox.Text = _settings.AccountEmail;
        PlayerRiotIdTextBox.Text = _settings.PreferredRiotId;
        AutoStartBackendCheckBox.IsChecked = _settings.AutoStartLocalBackend;
        UpdateAccountSessionUi();

        var overlaySelectionApplied = false;
        foreach (var item in OverlayPositionComboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), _settings.OverlayPosition.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                OverlayPositionComboBox.SelectedItem = item;
                overlaySelectionApplied = true;
                break;
            }
        }

        if (!overlaySelectionApplied)
        {
            OverlayPositionComboBox.SelectedIndex = 3;
        }

        var platformSelectionApplied = false;
        foreach (var item in PlayerPlatformRegionComboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), _settings.PreferredPlatformRegion, StringComparison.OrdinalIgnoreCase))
            {
                PlayerPlatformRegionComboBox.SelectedItem = item;
                platformSelectionApplied = true;
                break;
            }
        }

        if (!platformSelectionApplied)
        {
            PlayerPlatformRegionComboBox.SelectedIndex = 0;
        }

        UpdatePreparationHub(_lastSnapshot);
        UpdatePatchReview(_lastSnapshot);
    }

    private OverlayPosition GetSelectedOverlayPosition()
    {
        if (OverlayPositionComboBox.SelectedItem is ComboBoxItem item
            && Enum.TryParse<OverlayPosition>(item.Tag?.ToString(), out var position))
        {
            return position;
        }

        return OverlayPosition.BottomRight;
    }

    private string GetApiBaseUrlFromUi()
    {
        var value = ApiBaseUrlTextBox.Text?.Trim();
        return string.IsNullOrWhiteSpace(value) ? OverlaySettings.DefaultApiBaseUrl : value;
    }

    private string GetPlatformBaseUrlFromUi()
    {
        var value = PlatformBaseUrlTextBox.Text?.Trim();
        return string.IsNullOrWhiteSpace(value) ? OverlaySettings.DefaultPlatformBaseUrl : value;
    }

    private string GetAccountEmailFromUi()
    {
        return AccountEmailTextBox.Text?.Trim() ?? string.Empty;
    }

    private string GetPlayerRiotIdFromUi()
    {
        return PlayerRiotIdTextBox.Text?.Trim() ?? string.Empty;
    }

    private string GetPlayerPlatformRegionFromUi()
    {
        if (PlayerPlatformRegionComboBox.SelectedItem is ComboBoxItem item)
        {
            var value = item.Tag?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.ToUpperInvariant();
            }
        }

        return string.IsNullOrWhiteSpace(_settings.PreferredPlatformRegion)
            ? "EUW1"
            : _settings.PreferredPlatformRegion.ToUpperInvariant();
    }

    private string GetAccessKeyFromUi()
    {
        return _settings.AccessKey?.Trim() ?? string.Empty;
    }

    private OverlaySettings BuildSettingsFromUi(string? accessKey = null)
    {
        return new OverlaySettings
        {
            ApiBaseUrl = GetApiBaseUrlFromUi(),
            PlatformBaseUrl = GetPlatformBaseUrlFromUi(),
            AccountEmail = GetAccountEmailFromUi(),
            AccessKey = accessKey ?? _settings.AccessKey,
            PreferredRiotId = GetPlayerRiotIdFromUi(),
            PreferredPlatformRegion = GetPlayerPlatformRegionFromUi(),
            OverlayPosition = GetSelectedOverlayPosition(),
            AutoStartLocalBackend = AutoStartBackendCheckBox.IsChecked == true,
            OverlayLeft = _settings.OverlayLeft,
            OverlayTop = _settings.OverlayTop,
            HistoryLeft = _settings.HistoryLeft,
            HistoryTop = _settings.HistoryTop,
            BuildLeft = _settings.BuildLeft,
            BuildTop = _settings.BuildTop
        };
    }

    private async void OverlayWindow_PositionCommitted(double left, double top)
    {
        _settings.OverlayLeft = left;
        _settings.OverlayTop = top;
        await _settingsStore.SaveAsync(_settings);
    }

    private async void HistoryOverlayWindow_PositionCommitted(double left, double top)
    {
        _settings.HistoryLeft = left;
        _settings.HistoryTop = top;
        await _settingsStore.SaveAsync(_settings);
    }

    private async void BuildOverlayWindow_PositionCommitted(double left, double top)
    {
        _settings.BuildLeft = left;
        _settings.BuildTop = top;
        await _settingsStore.SaveAsync(_settings);
    }
}
