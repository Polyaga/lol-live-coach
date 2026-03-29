using System.Windows;
using System.Windows.Media;
using LolLiveCoach.Desktop.Models;

namespace LolLiveCoach.Desktop;

public partial class MainWindow
{
    private void UpdateAccessPanel(SubscriptionAccessDto access)
    {
        AccessStatusTitleText.Text = access.StatusTitle;
        AccessStatusDetailText.Text = access.StatusMessage;
        AccessTierBadgeText.Text = access.HasPremiumAccess ? "PREMIUM" : "FREE";
        AccessTierBadgeBorder.Background = access.HasPremiumAccess
            ? new SolidColorBrush(Color.FromRgb(11, 59, 43))
            : new SolidColorBrush(Color.FromRgb(27, 39, 64));
        AccessTierBadgeBorder.BorderBrush = access.HasPremiumAccess
            ? new SolidColorBrush(Color.FromRgb(49, 151, 107))
            : new SolidColorBrush(Color.FromRgb(36, 48, 66));

        FreePlanSummaryText.Foreground = access.HasPremiumAccess
            ? new SolidColorBrush(Color.FromRgb(148, 163, 184))
            : new SolidColorBrush(Color.FromRgb(248, 250, 252));
        PremiumPlanSummaryText.Foreground = access.HasPremiumAccess
            ? new SolidColorBrush(Color.FromRgb(248, 250, 252))
            : new SolidColorBrush(Color.FromRgb(148, 163, 184));

        UpgradeButton.Content = access.HasPremiumAccess ? "Voir l'offre active" : "Passer au premium";
        UpgradeButton.IsEnabled = !string.IsNullOrWhiteSpace(access.UpgradeUrl);

        ManageSubscriptionButton.Content = access.HasPremiumAccess ? "Gerer l'abonnement" : "Deja abonne ?";
        ManageSubscriptionButton.IsEnabled = !string.IsNullOrWhiteSpace(access.ManageSubscriptionUrl);

        if (!access.CanUseOverlayPreview && _overlayPreviewEnabled)
        {
            _overlayPreviewEnabled = false;

            if (_overlayWindow.IsVisible && !_isInGame)
            {
                _overlayWindow.Hide();
            }
        }

        UpdateOverlayPreviewButtonText();
        UpdatePreparationHub(_lastSnapshot);
        UpdatePatchReview(_lastSnapshot);
        UpdateDisplayMode(_isInGame);
    }

    private void UpdatePreparationHub(CoachSnapshot? snapshot = null)
    {
        var activeSnapshot = snapshot ?? _lastSnapshot;
        var accountConnected = !string.IsNullOrWhiteSpace(_settings.AccessKey);
        var overlayPositionLabel = FormatOverlayPosition(GetSelectedOverlayPosition());
        var playerProfileLabel = string.IsNullOrWhiteSpace(_playerProfile?.RiotId)
            ? string.IsNullOrWhiteSpace(GetPlayerRiotIdFromUi()) ? "A configurer" : GetPlayerRiotIdFromUi()
            : _playerProfile!.RiotId;

        RoleText.Text = playerProfileLabel;
        KdaText.Text = _playerProfile?.IsAvailable == true
            ? $"{BuildSoloQueueText(_playerProfile)} | {_playerProfile.RecentWinRate:0.#}% WR recent"
            : _overlayPreviewEnabled
                ? $"Overlay en {overlayPositionLabel} avec preview"
                : $"Overlay en {overlayPositionLabel}";

        PrepTitleText.Text = BuildPrepTitle(activeSnapshot, accountConnected);
        PrepDetailText.Text = BuildPrepDetail(activeSnapshot, accountConnected, overlayPositionLabel);
        NextActionText.Text = BuildNextAction(activeSnapshot, accountConnected, overlayPositionLabel);

        QuickStepOneTitleText.Text = _isBackendHealthy ? "Coach pret" : "Coach a reconnecter";
        QuickStepOneDetailText.Text = _isBackendHealthy
            ? _lastBackendDetail
            : "Le suivi live est temporairement coupe. Tu peux le relancer dans Parametres.";

        QuickStepTwoTitleText.Text = string.IsNullOrWhiteSpace(GetPlayerRiotIdFromUi()) ? "Profil joueur a ajouter" : "Profil joueur pret";
        QuickStepTwoDetailText.Text = string.IsNullOrWhiteSpace(GetPlayerRiotIdFromUi())
            ? "Ajoute ton Riot ID pour voir ton rang, ta forme recente et tes dernieres games dans ce hub."
            : _playerProfile?.IsAvailable == true
                ? BuildPlayerTrendText(_playerProfile)
                : _playerProfile?.Message ?? "Le profil sera charge automatiquement avec la prochaine mise a jour.";

        QuickStepThreeTitleText.Text = _overlayPreviewEnabled ? "Preview active" : "Overlay positionne";
        QuickStepThreeDetailText.Text = _overlayPreviewEnabled
            ? $"Preview visible en {overlayPositionLabel}. Ferme-la quand le placement te convient."
            : $"Overlay regle en {overlayPositionLabel}. Lance une preview rapide si tu veux valider le placement avant la queue.";

        LiveCommandText.Text = _currentAccess.CanUseNotificationHistory
            ? "En partie : maintiens Tab pour afficher l'historique recent et les rappels de build, puis fais glisser les panneaux si besoin."
            : _currentAccess.CanUseInGameOverlay
                ? $"En partie : l'overlay live se placera automatiquement en {overlayPositionLabel.ToLowerInvariant()} et restera non bloquant."
                : "En partie : le desktop restera ton hub principal. Passe au premium pour debloquer l'overlay live complet et les panneaux Tab.";
    }

    private string BuildPrepTitle(CoachSnapshot? snapshot, bool accountConnected)
    {
        if (string.IsNullOrWhiteSpace(GetPlayerRiotIdFromUi()))
        {
            return "Ajoute ton profil joueur pour personnaliser le hub.";
        }

        if (!_isBackendHealthy)
        {
            return "Le coach attend de reprendre le suivi live.";
        }

        if (snapshot?.Game.IsInGame == true)
        {
            return "Partie detectee, le live est en marche.";
        }

        if (accountConnected && (_currentAccess.CanUseOverlayPreview || _currentAccess.CanUseInGameOverlay))
        {
            return "Session prete a lancer.";
        }

        return accountConnected ? "Session presque prete." : "Le coach se met en place.";
    }

    private string BuildPrepDetail(CoachSnapshot? snapshot, bool accountConnected, string overlayPositionLabel)
    {
        if (string.IsNullOrWhiteSpace(GetPlayerRiotIdFromUi()))
        {
            return $"Entre ton Riot ID, choisis ta region puis garde l'overlay en {overlayPositionLabel} pour preparer la prochaine game.";
        }

        if (!_isBackendHealthy)
        {
            return "Le hub reste consultable, mais les conseils live reviendront une fois le coach reconnecte.";
        }

        if (snapshot?.Game.IsInGame == true)
        {
            var champion = snapshot.Game.LocalPlayer?.ChampionName;
            var phase = snapshot.Advice.GamePhase ?? "live";
            return champion is null
                ? $"Le coach suit deja la partie en {phase}."
                : $"Le coach suit {champion} en {phase} et laisse deja l'overlay prendre le relais.";
        }

        if (!accountConnected)
        {
            return $"Le coach est pret. Connecte ton compte puis garde l'overlay en {overlayPositionLabel} pour lancer ta prochaine game sereinement.";
        }

        return _overlayPreviewEnabled
            ? $"Preview active en {overlayPositionLabel}. Quand le coin te convient, tu peux lancer ta prochaine partie."
            : $"Le coach tourne, le compte est reconnu et l'overlay est cale en {overlayPositionLabel}.";
    }

    private string BuildNextAction(CoachSnapshot? snapshot, bool accountConnected, string overlayPositionLabel)
    {
        if (string.IsNullOrWhiteSpace(GetPlayerRiotIdFromUi()))
        {
            return "Prochaine action : ajoute ton Riot ID pour afficher ton rang, tes parties recentes et ta forme du moment.";
        }

        if (!_isBackendHealthy)
        {
            return "Prochaine action : relance le suivi live depuis Parametres pour retrouver les conseils en direct.";
        }

        if (!accountConnected)
        {
            return "Prochaine action : connecte ton compte pour verifier ton acces et retrouver ton setup.";
        }

        if (!_overlayPreviewEnabled && _currentAccess.CanUseOverlayPreview)
        {
            return $"Prochaine action : lance une preview rapide pour valider l'overlay en {overlayPositionLabel}.";
        }

        return snapshot?.Game.IsInGame == true
            ? "Prochaine action : joue, le desktop se masque et l'overlay prend le relais."
            : "Prochaine action : tu peux lancer le client LoL, le coach basculera automatiquement en overlay des qu'une partie demarre.";
    }

    private void UpdatePatchReview(CoachSnapshot? snapshot = null)
    {
        var activeSnapshot = snapshot ?? _lastSnapshot;
        var focusItems = BuildPatchFocusItems(activeSnapshot);
        var championPoolItems = BuildChampionPoolItems();

        PatchHeadlineText.Text = BuildPatchHeadline(activeSnapshot);
        PatchSummaryText.Text = BuildPatchSummary(activeSnapshot);

        PatchFocusItemsControl.ItemsSource = focusItems;
        EmptyPatchFocusText.Text = "Ajoute ton Riot ID et quelques parties recentes pour nourrir automatiquement cette page.";
        EmptyPatchFocusText.Visibility = focusItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        PatchChampionPoolItemsControl.ItemsSource = championPoolItems;
        EmptyPatchChampionPoolText.Text = "Ton pool recent apparaitra ici des que le profil joueur sera charge.";
        EmptyPatchChampionPoolText.Visibility = championPoolItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private string BuildPatchHeadline(CoachSnapshot? snapshot)
    {
        if (string.IsNullOrWhiteSpace(GetPlayerRiotIdFromUi()))
        {
            return "Cette page devient vraiment utile quand ton profil joueur est renseigne.";
        }

        if (snapshot?.Game.IsInGame == true)
        {
            var champion = snapshot.Game.LocalPlayer?.ChampionName ?? "ton champion";
            return $"Lecture live autour de {champion}.";
        }

        if (_playerProfile?.IsAvailable == true)
        {
            return "Le meta du moment, filtre par ta forme recente.";
        }

        return _isBackendHealthy
            ? "Le coach prepare deja une revue perso de ton meta."
            : "Revue meta en attente du retour du coach.";
    }

    private string BuildPatchSummary(CoachSnapshot? snapshot)
    {
        if (_playerProfile?.IsAvailable == true)
        {
            return $"On melange tes dernieres games, ton pool recent et les conseils du coach pour te donner une lecture actionnable avant la queue. {BuildPlayerTrendText(_playerProfile)}";
        }

        if (!_isBackendHealthy)
        {
            return "Le hub reste consultable, mais les recommandations dynamiques reviendront quand le suivi live sera reconnecte.";
        }

        return snapshot?.Advice.MainAdvice is { Length: > 0 } mainAdvice
            ? $"Le coach se base deja sur le contexte actuel : {mainAdvice}"
            : "La page patchs se remplit automatiquement a mesure que le coach et ton profil recuperent plus de contexte.";
    }

    private List<string> BuildPatchFocusItems(CoachSnapshot? snapshot)
    {
        var items = new List<string>();

        if (_playerProfile?.IsAvailable == true && _playerProfile.RecentSampleSize > 0)
        {
            items.Add(_playerProfile.RecentWinRate >= 55
                ? $"Tu arrives avec {_playerProfile.RecentWinRate:0.#}% de winrate recent : garde confiance sur tes picks confort."
                : $"Ton winrate recent est a {_playerProfile.RecentWinRate:0.#}% : cherche un plan de jeu simple et stable sur la prochaine serie.");

            items.Add(_playerProfile.RecentCsPerMinute >= 6.5
                ? $"Ton farming tient la route ({_playerProfile.RecentCsPerMinute:0.#} cs/min) : tu peux jouer un peu plus pour convertir l'avantage."
                : $"Ton farm recent est a {_playerProfile.RecentCsPerMinute:0.#} cs/min : priorise une early game plus propre avant de chercher les plays difficiles.");
        }

        foreach (var alert in (snapshot?.Advice.Alerts ?? []).Take(2))
        {
            items.Add($"A surveiller : {alert}");
        }

        foreach (var recommendation in (snapshot?.Advice.ItemRecommendations ?? []).Take(2))
        {
            items.Add($"Build : {recommendation.ItemName} pour {recommendation.Reason}");
        }

        return items.Take(5).ToList();
    }

    private List<string> BuildChampionPoolItems()
    {
        if (_playerProfile?.IsAvailable != true)
        {
            return [];
        }

        return _playerProfile.RecentMatches
            .Where(match => !string.IsNullOrWhiteSpace(match.ChampionName))
            .GroupBy(match => match.ChampionName)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Count(match => match.Win))
            .Take(4)
            .Select(BuildChampionPoolLine)
            .ToList();
    }

    private static string BuildChampionPoolLine(IGrouping<string, PlayerRecentMatchDto> group)
    {
        var games = group.ToList();
        var wins = games.Count(match => match.Win);
        var totalDeaths = games.Sum(match => match.Deaths);
        var totalKillsAndAssists = games.Sum(match => match.Kills + match.Assists);
        var kda = totalKillsAndAssists / (double)Math.Max(1, totalDeaths);
        var winRate = wins * 100d / games.Count;

        return $"{group.Key} | {games.Count} game(s) | {winRate:0.#}% WR | {kda:0.0} KDA";
    }

    private static string BuildCoachStatusTitle(BackendLaunchStatus status)
    {
        if (status.IsHealthy)
        {
            return "Coach pret";
        }

        return status.Title switch
        {
            "Demarrage automatique desactive" => "Coach en attente",
            "Backend distant ou manuel" => "Coach a connecter",
            "Redemarrage backend..." => "Coach en relance",
            _ => "Coach indisponible"
        };
    }

    private static string BuildCoachStatusDetail(BackendLaunchStatus status)
    {
        if (status.IsHealthy)
        {
            return "Le suivi live, le profil joueur et les conseils sont prets pour la prochaine game.";
        }

        return status.Title switch
        {
            "Demarrage automatique desactive" => "Active le redemarrage auto ou utilise le bouton de relance dans Parametres.",
            "Backend distant ou manuel" => "Le coach attend son service de suivi. Tu peux le reconnecter depuis Parametres.",
            "Redemarrage backend..." => "Le coach relance son suivi live.",
            _ => "Le hub reste visible, mais le suivi live reviendra quand le coach sera reconnecte."
        };
    }

    private static string BuildSoloQueueText(PlayerProfileDto profile)
    {
        if (string.IsNullOrWhiteSpace(profile.SoloQueueTier) || string.Equals(profile.SoloQueueTier, "Non classe", StringComparison.OrdinalIgnoreCase))
        {
            return "Non classe";
        }

        return profile.SoloQueueLeaguePoints > 0
            ? $"{profile.SoloQueueTier} - {profile.SoloQueueLeaguePoints} LP"
            : profile.SoloQueueTier;
    }

    private static string BuildPlayerTrendText(PlayerProfileDto profile)
    {
        if (!profile.IsAvailable)
        {
            return profile.Message;
        }

        if (profile.RecentSampleSize == 0)
        {
            return $"{BuildSoloQueueText(profile)}. Lance quelques parties pour afficher la forme recente.";
        }

        var record = profile.SoloQueueWins + profile.SoloQueueLosses > 0
            ? $" SoloQ {profile.SoloQueueWins}V/{profile.SoloQueueLosses}D."
            : string.Empty;

        return $"{profile.RecentSampleSize} parties recentes, {profile.RecentCsPerMinute:0.#} cs/min et {profile.RecentWinRate:0.#}% de winrate.{record}";
    }

    private static string FormatOverlayPosition(OverlayPosition position) => position switch
    {
        OverlayPosition.TopLeft => "Haut gauche",
        OverlayPosition.TopRight => "Haut droite",
        OverlayPosition.BottomLeft => "Bas gauche",
        _ => "Bas droite"
    };
}
