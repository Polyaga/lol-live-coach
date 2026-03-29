using System.Windows;
using LolLiveCoach.Desktop.Models;

namespace LolLiveCoach.Desktop;

public partial class MainWindow
{
    private async void RefreshPlayerProfileButton_Click(object sender, RoutedEventArgs e)
    {
        _settings = BuildSettingsFromUi(_settings.AccessKey);
        await _settingsStore.SaveAsync(_settings);
        await RefreshPlayerProfileIfNeededAsync(force: true);
        UpdatePreparationHub(_lastSnapshot);
    }

    private async Task RefreshPlayerProfileIfNeededAsync(bool force = false)
    {
        var riotId = GetPlayerRiotIdFromUi();
        var platformRegion = GetPlayerPlatformRegionFromUi();

        if (string.IsNullOrWhiteSpace(riotId))
        {
            _playerProfile = null;
            UpdatePlayerProfileUi();
            return;
        }

        if (!_isBackendHealthy)
        {
            if (_playerProfile is null)
            {
                UpdatePlayerProfileUi(new PlayerProfileDto
                {
                    IsConfigured = true,
                    IsAvailable = false,
                    RiotId = riotId,
                    PlatformRegion = platformRegion,
                    Message = "Le profil joueur sera charge des que le backend local repondra."
                });
            }

            return;
        }

        if (_isRefreshingPlayerProfile)
        {
            return;
        }

        if (!force && _playerProfile is not null && DateTimeOffset.UtcNow < _nextPlayerProfileRefreshAt)
        {
            UpdatePlayerProfileUi(_playerProfile);
            return;
        }

        try
        {
            _isRefreshingPlayerProfile = true;
            RefreshPlayerProfileButton.IsEnabled = false;
            RefreshPlayerProfileButton.Content = "Actualisation...";
            UpdatePlayerProfileUi(new PlayerProfileDto
            {
                IsConfigured = true,
                IsAvailable = false,
                RiotId = riotId,
                PlatformRegion = platformRegion,
                Message = "Recuperation du profil joueur en cours..."
            });

            _playerProfile = await _apiClient.GetPlayerProfileAsync(riotId, platformRegion);
            _nextPlayerProfileRefreshAt = DateTimeOffset.UtcNow.AddMinutes(_playerProfile.IsAvailable ? 2 : 1);
            UpdatePlayerProfileUi(_playerProfile);
            UpdatePreparationHub(_lastSnapshot);
        }
        catch (Exception ex)
        {
            _playerProfile = new PlayerProfileDto
            {
                IsConfigured = true,
                IsAvailable = false,
                RiotId = riotId,
                PlatformRegion = platformRegion,
                Message = ex.Message
            };
            _nextPlayerProfileRefreshAt = DateTimeOffset.UtcNow.AddSeconds(30);
            UpdatePlayerProfileUi(_playerProfile);
            UpdatePreparationHub(_lastSnapshot);
        }
        finally
        {
            _isRefreshingPlayerProfile = false;
            RefreshPlayerProfileButton.IsEnabled = true;
            RefreshPlayerProfileButton.Content = "Mettre a jour";
        }
    }

    private void UpdatePlayerProfileUi(PlayerProfileDto? profile = null)
    {
        var activeProfile = profile ?? _playerProfile;
        var configuredRiotId = GetPlayerRiotIdFromUi();

        if (string.IsNullOrWhiteSpace(configuredRiotId))
        {
            PlayerProfileStatusText.Text = "A configurer";
            PlayerProfileSummaryText.Text = "Ajoute ton Riot ID pour voir ton rang, ta forme recente et tes dernieres parties.";
            PlayerRankText.Text = "Non classe";
            PlayerLevelText.Text = "-";
            PlayerRecentKdaText.Text = "-";
            PlayerRecentWinRateText.Text = "-";
            PlayerTrendText.Text = "Exemple : NomDeJoueur#TAG.";
            RecentMatchesItemsControl.ItemsSource = Array.Empty<PlayerRecentMatchDto>();
            EmptyPlayerMatchesText.Text = "Aucune partie recente a afficher pour le moment.";
            EmptyPlayerMatchesText.Visibility = Visibility.Visible;
            UpdatePreparationHub(_lastSnapshot);
            UpdatePatchReview(_lastSnapshot);
            return;
        }

        if (activeProfile is null)
        {
            PlayerProfileStatusText.Text = "En attente";
            PlayerProfileSummaryText.Text = "Le profil sera charge automatiquement apres le prochain refresh.";
            PlayerRankText.Text = "-";
            PlayerLevelText.Text = "-";
            PlayerRecentKdaText.Text = "-";
            PlayerRecentWinRateText.Text = "-";
            PlayerTrendText.Text = "Le coach preparera les stats recentes ici.";
            RecentMatchesItemsControl.ItemsSource = Array.Empty<PlayerRecentMatchDto>();
            EmptyPlayerMatchesText.Text = "Aucune partie recente a afficher pour le moment.";
            EmptyPlayerMatchesText.Visibility = Visibility.Visible;
            UpdatePreparationHub(_lastSnapshot);
            UpdatePatchReview(_lastSnapshot);
            return;
        }

        PlayerProfileStatusText.Text = activeProfile.IsAvailable
            ? activeProfile.PlatformRegion
            : activeProfile.IsConfigured ? "Indisponible" : "Cle manquante";
        PlayerProfileSummaryText.Text = activeProfile.IsAvailable
            ? $"{activeProfile.RiotId} sur {activeProfile.PlatformRegion}."
            : activeProfile.Message;
        PlayerRankText.Text = activeProfile.IsAvailable
            ? BuildSoloQueueText(activeProfile)
            : "Non charge";
        PlayerLevelText.Text = activeProfile.IsAvailable && activeProfile.SummonerLevel > 0
            ? $"Nv. {activeProfile.SummonerLevel}"
            : "-";
        PlayerRecentKdaText.Text = activeProfile.IsAvailable && activeProfile.RecentSampleSize > 0
            ? activeProfile.RecentKda.ToString("0.00")
            : "-";
        PlayerRecentWinRateText.Text = activeProfile.IsAvailable && activeProfile.RecentSampleSize > 0
            ? $"{activeProfile.RecentWinRate:0.#}%"
            : "-";
        PlayerTrendText.Text = activeProfile.IsAvailable
            ? BuildPlayerTrendText(activeProfile)
            : activeProfile.Message;

        var recentMatches = activeProfile.IsAvailable
            ? activeProfile.RecentMatches.Take(5).ToList()
            : new List<PlayerRecentMatchDto>();

        RecentMatchesItemsControl.ItemsSource = recentMatches;
        EmptyPlayerMatchesText.Text = activeProfile.IsAvailable
            ? "Aucune partie recente a afficher pour le moment."
            : activeProfile.Message;
        EmptyPlayerMatchesText.Visibility = recentMatches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdatePreparationHub(_lastSnapshot);
        UpdatePatchReview(_lastSnapshot);
    }
}
