using System.Windows;
using System.Windows.Threading;
using LolLiveCoach.Desktop.Models;
using LolLiveCoach.Desktop.Services;

namespace LolLiveCoach.Desktop;

public partial class MainWindow : Window
{
    private readonly CoachApiClient _apiClient = new();
    private readonly PlatformAccountClient _platformAccountClient = new();
    private readonly BackendProcessManager _backendProcessManager = new();
    private readonly AppUpdateService _appUpdateService = new();
    private readonly NotificationFeed _notificationFeed = new();
    private readonly GlobalTabKeyMonitor _tabKeyMonitor = new();
    private readonly OverlaySettingsStore _settingsStore = new();
    private readonly OverlayWindow _overlayWindow = new();
    private readonly NotificationBubbleWindow _notificationBubbleWindow = new();
    private readonly HistoryOverlayWindow _historyOverlayWindow = new();
    private readonly BuildOverlayWindow _buildOverlayWindow = new();
    private readonly DispatcherTimer _refreshTimer;
    private OverlaySettings _settings = OverlaySettings.CreateDefault();
    private SubscriptionAccessDto _currentAccess = SubscriptionAccessDto.CreateFree();
    private CoachSnapshot? _lastSnapshot;
    private PlayerProfileDto? _playerProfile;
    private bool _isInGame;
    private bool _isRefreshing;
    private bool _overlayPreviewEnabled;
    private bool _isBackendHealthy;
    private string _lastBackendDetail = "Le coach se prepare pour la prochaine session.";
    private bool _isRefreshingPlayerProfile;
    private DateTimeOffset _nextPlayerProfileRefreshAt = DateTimeOffset.MinValue;

    public MainWindow()
    {
        InitializeComponent();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };

        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        _refreshTimer.Tick += RefreshTimer_Tick;
        _tabKeyMonitor.TabStateChanged += TabKeyMonitor_TabStateChanged;
        _overlayWindow.PositionCommitted += OverlayWindow_PositionCommitted;
        _historyOverlayWindow.PositionCommitted += HistoryOverlayWindow_PositionCommitted;
        _buildOverlayWindow.PositionCommitted += BuildOverlayWindow_PositionCommitted;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = await _settingsStore.LoadAsync();
        ApplySettingsToUi();
        UpdatePlayerProfileUi();
        UpdateAccessPanel(_currentAccess);
        UpdateOverlayPreviewButtonText();
        UpdatePreparationHub();
        UpdatePatchReview();
        _overlayWindow.ApplySettings(_settings);
        _notificationBubbleWindow.ApplySettings(_settings);
        _historyOverlayWindow.ApplySettings(_settings);
        _buildOverlayWindow.ApplySettings(_settings);
        _historyOverlayWindow.UpdateEntries(_notificationFeed.Entries);
        _tabKeyMonitor.Start();

        _refreshTimer.Start();
        await RefreshAsync();
        _ = _appUpdateService.CheckForUpdatesInBackgroundAsync();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
        _overlayWindow.Close();
        _notificationBubbleWindow.Close();
        _historyOverlayWindow.Close();
        _buildOverlayWindow.Close();
        _tabKeyMonitor.Dispose();
        _backendProcessManager.Dispose();
        _apiClient.Dispose();
        _platformAccountClient.Dispose();
    }
}
