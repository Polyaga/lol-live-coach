using System.Windows;
using System.Windows.Threading;
using LolLiveCoach.Desktop.Models;
using LolLiveCoach.Desktop.Services;

namespace LolLiveCoach.Desktop;

public partial class NotificationBubbleWindow : Window
{
    private const double WindowMargin = 24;
    private const double BubbleGap = 18;
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    public NotificationBubbleWindow()
    {
        InitializeComponent();
        _hideTimer.Tick += HideTimer_Tick;
    }

    public void ApplySettings(OverlaySettings settings)
    {
        PositionWindow(settings);
    }

    public void ShowEntry(NotificationEntry entry, OverlaySettings settings)
    {
        BubbleTimeText.Text = entry.Title;
        BubbleMessageText.Text = entry.Message;
        BubbleSubMessageText.Text = entry.SubMessage ?? string.Empty;
        BubbleSubMessageText.Visibility = string.IsNullOrWhiteSpace(entry.SubMessage)
            ? Visibility.Collapsed
            : Visibility.Visible;

        PositionWindow(settings);

        _hideTimer.Stop();

        if (!IsVisible)
        {
            Show();
        }

        _hideTimer.Start();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        FloatingWindowChrome.Apply(this, interactive: false);
    }

    private void HideTimer_Tick(object? sender, EventArgs e)
    {
        _hideTimer.Stop();
        Hide();
    }

    private void PositionWindow(OverlaySettings settings)
    {
        UpdateLayout();

        if (settings.OverlayLeft.HasValue && settings.OverlayTop.HasValue)
        {
            Left = settings.OverlayLeft.Value;
            Top = settings.OverlayTop.Value - ActualHeight - BubbleGap;
            FloatingWindowChrome.ClampToWorkArea(this);
            return;
        }

        var workArea = FloatingWindowChrome.GetPlacementArea(this);
        Left = settings.OverlayPosition is OverlayPosition.TopLeft or OverlayPosition.BottomLeft
            ? workArea.Left + WindowMargin
            : workArea.Right - Width - WindowMargin;
        Top = settings.OverlayPosition switch
        {
            OverlayPosition.TopLeft or OverlayPosition.TopRight => workArea.Top + WindowMargin + BubbleGap,
            OverlayPosition.BottomLeft or OverlayPosition.BottomRight => workArea.Bottom - ActualHeight - WindowMargin - 170 - BubbleGap,
            _ => workArea.Bottom - ActualHeight - WindowMargin - BubbleGap
        };
    }
}
