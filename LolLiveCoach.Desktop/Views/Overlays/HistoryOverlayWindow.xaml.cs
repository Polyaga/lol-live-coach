using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LolLiveCoach.Desktop.Models;
using LolLiveCoach.Desktop.Services;

namespace LolLiveCoach.Desktop;

public partial class HistoryOverlayWindow : Window
{
    private const double WindowMargin = 24;
    private const int MaxVisibleEntries = 4;
    private bool _isInteractive;

    public HistoryOverlayWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += HistoryOverlayWindow_MouseLeftButtonDown;
    }

    public event Action<double, double>? PositionCommitted;

    public void ApplySettings(OverlaySettings settings)
    {
        PositionWindow(settings);
    }

    public void UpdateEntries(ObservableCollection<NotificationEntry> entries)
    {
        HistoryItemsControl.ItemsSource = entries.Take(MaxVisibleEntries).ToList();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        FloatingWindowChrome.Apply(this, interactive: false);
    }

    public void SetInteractive(bool isInteractive)
    {
        _isInteractive = isInteractive;
        Cursor = isInteractive ? Cursors.SizeAll : Cursors.Arrow;
        FloatingWindowChrome.Apply(this, isInteractive);
    }

    private void HistoryOverlayWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isInteractive || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
            FloatingWindowChrome.ClampToWorkArea(this);
            PositionCommitted?.Invoke(Left, Top);
        }
        catch
        {
            // DragMove can throw if the mouse state changes mid-drag.
        }
    }

    private void PositionWindow(OverlaySettings settings)
    {
        if (settings.HistoryLeft.HasValue && settings.HistoryTop.HasValue)
        {
            Left = settings.HistoryLeft.Value;
            Top = settings.HistoryTop.Value;
            FloatingWindowChrome.ClampToWorkArea(this);
            return;
        }

        var workArea = SystemParameters.WorkArea;
        Left = settings.OverlayPosition is OverlayPosition.TopLeft or OverlayPosition.BottomLeft
            ? workArea.Left + WindowMargin
            : workArea.Right - Width - WindowMargin;
        Top = workArea.Top + 100;
    }
}
