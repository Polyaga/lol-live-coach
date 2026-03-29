using System.Windows;
using System.Windows.Input;
using LolLiveCoach.Desktop.Models;
using LolLiveCoach.Desktop.Services;

namespace LolLiveCoach.Desktop;

public partial class BuildOverlayWindow : Window
{
    private const double WindowMargin = 24;
    private bool _isInteractive;

    public BuildOverlayWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += BuildOverlayWindow_MouseLeftButtonDown;
    }

    public event Action<double, double>? PositionCommitted;

    public void ApplySettings(OverlaySettings settings)
    {
        PositionWindow(settings);
    }

    public void UpdateTips(IEnumerable<ItemRecommendationDto> itemRecommendations)
    {
        var recommendations = itemRecommendations
            .Where(recommendation => !string.IsNullOrWhiteSpace(recommendation.ItemName))
            .Take(4)
            .ToList();
        var hasPriorityTips = recommendations.Count > 0;

        if (!hasPriorityTips)
        {
            recommendations =
            [
                new ItemRecommendationDto
                {
                    ItemName = "Build stable",
                    Category = "Lecture",
                    Reason = "Pas de spike de stuff ultra prioritaire detecte pour l'instant.",
                    PurchaseHint = "Le coach te proposera ici anti-heal, anti-tank, survie ou resistance quand le draft l'exige."
                }
            ];
        }

        BuildTitleText.Text = hasPriorityTips ? "Priorites d'achat" : "Build stable";
        BuildSubtitleText.Text = hasPriorityTips
            ? "Recommandations basees sur la game en cours."
            : "Le panneau se remplit des qu'un achat situationnel devient clef.";
        BuildTipsItemsControl.ItemsSource = recommendations;
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

    private void BuildOverlayWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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
        if (settings.BuildLeft.HasValue && settings.BuildTop.HasValue)
        {
            Left = settings.BuildLeft.Value;
            Top = settings.BuildTop.Value;
            FloatingWindowChrome.ClampToWorkArea(this);
            return;
        }

        var workArea = SystemParameters.WorkArea;
        var alignLeft = settings.OverlayPosition is OverlayPosition.TopRight or OverlayPosition.BottomRight;
        Left = alignLeft
            ? workArea.Left + WindowMargin
            : workArea.Right - Width - WindowMargin;
        Top = workArea.Top + 100;
    }
}
