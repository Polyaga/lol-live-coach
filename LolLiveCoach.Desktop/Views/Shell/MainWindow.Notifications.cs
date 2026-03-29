using System.Windows;
using LolLiveCoach.Desktop.Models;

namespace LolLiveCoach.Desktop;

public partial class MainWindow
{
    private void ProcessNotifications(CoachSnapshot snapshot)
    {
        if (!snapshot.Access.CanUseNotificationBubbles && !snapshot.Access.CanUseNotificationHistory)
        {
            _notificationFeed.Clear();
            _historyOverlayWindow.UpdateEntries(_notificationFeed.Entries);
            _buildOverlayWindow.UpdateTips(Array.Empty<ItemRecommendationDto>());

            if (_notificationBubbleWindow.IsVisible)
            {
                _notificationBubbleWindow.Hide();
            }

            _historyOverlayWindow.SetInteractive(false);
            _buildOverlayWindow.SetInteractive(false);
            return;
        }

        var entry = _notificationFeed.PushIfChanged(snapshot);
        _historyOverlayWindow.UpdateEntries(_notificationFeed.Entries);
        _buildOverlayWindow.UpdateTips(snapshot.Advice.ItemRecommendations ?? new List<ItemRecommendationDto>());

        if (entry is null || !snapshot.Access.CanUseNotificationBubbles)
        {
            return;
        }

        _notificationBubbleWindow.ApplySettings(_settings);
        _notificationBubbleWindow.ShowEntry(entry, _settings);
    }

    private void TabKeyMonitor_TabStateChanged(bool isPressed)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_isInGame || !_currentAccess.CanUseNotificationHistory)
            {
                _overlayWindow.SetInteractive(false);
                _historyOverlayWindow.SetInteractive(false);
                _buildOverlayWindow.SetInteractive(false);

                if (_historyOverlayWindow.IsVisible)
                {
                    _historyOverlayWindow.Hide();
                }

                if (_buildOverlayWindow.IsVisible)
                {
                    _buildOverlayWindow.Hide();
                }

                return;
            }

            if (isPressed)
            {
                _overlayWindow.SetInteractive(true);

                if (_notificationFeed.Entries.Count > 0)
                {
                    _historyOverlayWindow.ApplySettings(_settings);
                    _historyOverlayWindow.UpdateEntries(_notificationFeed.Entries);
                    _historyOverlayWindow.SetInteractive(true);

                    if (!_historyOverlayWindow.IsVisible)
                    {
                        _historyOverlayWindow.Show();
                    }
                }

                var itemRecommendations = _lastSnapshot?.Advice.ItemRecommendations ?? new List<ItemRecommendationDto>();
                if (itemRecommendations.Count > 0)
                {
                    _buildOverlayWindow.ApplySettings(_settings);
                    _buildOverlayWindow.UpdateTips(itemRecommendations);
                    _buildOverlayWindow.SetInteractive(true);

                    if (!_buildOverlayWindow.IsVisible)
                    {
                        _buildOverlayWindow.Show();
                    }
                }
            }
            else
            {
                _overlayWindow.SetInteractive(false);
                _historyOverlayWindow.SetInteractive(false);
                _buildOverlayWindow.SetInteractive(false);

                if (_historyOverlayWindow.IsVisible)
                {
                    _historyOverlayWindow.Hide();
                }

                if (_buildOverlayWindow.IsVisible)
                {
                    _buildOverlayWindow.Hide();
                }
            }
        });
    }

    private void UpdateOverlayPreviewButtonText()
    {
        OverlayPreviewButton.IsEnabled = _currentAccess.CanUseOverlayPreview;
        OverlayPreviewButton.Content = !_currentAccess.CanUseOverlayPreview
            ? "Preview indisponible"
            : _overlayPreviewEnabled
                ? _currentAccess.HasPremiumAccess ? "Masquer la preview" : "Masquer la preview premium"
                : _currentAccess.HasPremiumAccess ? "Previsualiser l'overlay" : "Previsualiser l'overlay premium";
    }
}
