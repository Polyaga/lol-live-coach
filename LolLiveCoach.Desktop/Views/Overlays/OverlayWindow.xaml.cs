using System.Windows;
using System.Windows.Input;
using LolLiveCoach.Desktop.Models;
using LolLiveCoach.Desktop.Services;

namespace LolLiveCoach.Desktop;

public partial class OverlayWindow : Window
{
    private const double WindowMargin = 24;
    private bool _isInteractive;

    public OverlayWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += OverlayWindow_MouseLeftButtonDown;
    }

    public event Action<double, double>? PositionCommitted;

    public void ApplySettings(OverlaySettings settings)
    {
        PositionWindow(settings);
    }

    public void UpdateSnapshot(CoachSnapshot snapshot)
    {
        var localPlayer = snapshot.Game.LocalPlayer;
        var activePlayer = snapshot.Game.ActivePlayer;
        var alerts = snapshot.Advice.Alerts?.Take(3).ToList() ?? [];
        var resourceText = BuildResourceStatus(activePlayer);

        OverlayPhaseText.Text = snapshot.Advice.GamePhase ?? "Live";
        OverlayMainAdviceText.Text = snapshot.Advice.MainAdvice;
        OverlaySecondaryAdviceText.Text = snapshot.Advice.SecondaryAdvice ?? "Lecture contextuelle en cours.";
        OverlayRoleText.Text = $"{FormatRole(snapshot.Game.DetectedRole)} | {localPlayer?.ChampionName ?? "Champion"}";
        OverlayKdaText.Text = BuildStatusLine(localPlayer, resourceText);
        OverlayAlertsItemsControl.ItemsSource = alerts;
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

    private void OverlayWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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
        if (settings.OverlayLeft.HasValue && settings.OverlayTop.HasValue)
        {
            Left = settings.OverlayLeft.Value;
            Top = settings.OverlayTop.Value;
            FloatingWindowChrome.ClampToWorkArea(this);
            return;
        }

        var workArea = FloatingWindowChrome.GetPlacementArea(this);
        Left = settings.OverlayPosition is OverlayPosition.TopLeft or OverlayPosition.BottomLeft
            ? workArea.Left + WindowMargin
            : workArea.Right - Width - WindowMargin;
        Top = settings.OverlayPosition is OverlayPosition.TopLeft or OverlayPosition.TopRight
            ? workArea.Top + WindowMargin
            : workArea.Bottom - Height - WindowMargin;
    }

    private static string FormatRole(int role) => role switch
    {
        1 => "Top",
        2 => "Jungle",
        3 => "Mid",
        4 => "ADC",
        5 => "Support",
        _ => "Role inconnu"
    };

    private static string BuildStatusLine(PlayerSummaryDto? localPlayer, string? resourceText)
    {
        var kdaText = localPlayer is null
            ? "KDA indisponible"
            : $"KDA {localPlayer.Kills}/{localPlayer.Deaths}/{localPlayer.Assists}";

        return string.IsNullOrWhiteSpace(resourceText)
            ? kdaText
            : $"{kdaText} | {resourceText}";
    }

    private static string? BuildResourceStatus(ActivePlayerDto? activePlayer)
    {
        if (activePlayer is null || activePlayer.MaxMana <= 0)
        {
            return null;
        }

        var label = activePlayer.ResourceType?.ToUpperInvariant() switch
        {
            "MANA" => "Mana",
            "ENERGY" => "Energie",
            _ => null
        };

        if (label is null)
        {
            return null;
        }

        var ratio = activePlayer.CurrentMana / activePlayer.MaxMana;
        return $"{label} {Math.Round(ratio * 100)}%";
    }
}
