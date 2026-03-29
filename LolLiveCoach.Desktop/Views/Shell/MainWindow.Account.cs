using System.Diagnostics;
using System.Windows;

namespace LolLiveCoach.Desktop;

public partial class MainWindow
{
    private async void LoginAccountButton_Click(object sender, RoutedEventArgs e)
    {
        var email = GetAccountEmailFromUi();
        var password = AccountPasswordBox.Password?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show(
                "Renseigne l'email et le mot de passe du compte client avant de te connecter.",
                "Compte incomplet",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            AccountSessionStatusText.Text = "Connexion a la plateforme en cours...";
            _platformAccountClient.SetBaseAddress(GetPlatformBaseUrlFromUi());
            var result = await _platformAccountClient.LoginAsync(email, password, "Desktop Windows");
            _settings = BuildSettingsFromUi(result.Token);
            await _settingsStore.SaveAsync(_settings);
            AccountPasswordBox.Clear();
            UpdateAccountSessionUi(result.User?.Email ?? email);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            UpdateAccountSessionUi();
            MessageBox.Show(
                ex.Message,
                "Connexion impossible",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void LogoutAccountButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _platformAccountClient.SetBaseAddress(GetPlatformBaseUrlFromUi());
            await _platformAccountClient.LogoutAsync(_settings.AccessKey);
        }
        catch
        {
            // Best effort logout. We still clear the local token below.
        }

        _settings = BuildSettingsFromUi(string.Empty);
        await _settingsStore.SaveAsync(_settings);
        AccountPasswordBox.Clear();
        UpdateAccountSessionUi();
        await RefreshAsync();
    }

    private void UpgradeButton_Click(object sender, RoutedEventArgs e)
    {
        OpenConfiguredUrl(
            _currentAccess.UpgradeUrl,
            "Ajoute l'URL de l'offre dans la section Freemium du backend pour activer ce bouton.");
    }

    private void ManageSubscriptionButton_Click(object sender, RoutedEventArgs e)
    {
        OpenConfiguredUrl(
            _currentAccess.ManageSubscriptionUrl,
            "Ajoute l'URL du portail client ou de la gestion d'abonnement dans la configuration Freemium.");
    }

    private static void OpenConfiguredUrl(string? url, string missingMessage)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show(
                missingMessage,
                "Lien d'abonnement non configure",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            MessageBox.Show(
                "Impossible d'ouvrir le lien configure pour le moment.",
                "Ouverture impossible",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void UpdateAccountSessionUi(string? email = null)
    {
        var accountEmail = string.IsNullOrWhiteSpace(email) ? _settings.AccountEmail : email;
        var isConnected = !string.IsNullOrWhiteSpace(_settings.AccessKey);

        AccountSessionStatusText.Text = isConnected
            ? "Ton compte est deja connecte. Tu peux simplement continuer ou te deconnecter pour changer de session."
            : "Aucune session compte active. Connecte-toi pour retrouver ton acces premium.";
        ConnectedAccountEmailText.Text = string.IsNullOrWhiteSpace(accountEmail) ? "Compte connecte" : accountEmail;
        AccountLoginFormPanel.Visibility = isConnected ? Visibility.Collapsed : Visibility.Visible;
        AccountConnectedPanel.Visibility = isConnected ? Visibility.Visible : Visibility.Collapsed;
        AccountEmailTextBox.IsEnabled = !isConnected;
        AccountPasswordBox.IsEnabled = !isConnected;
        LoginAccountButton.IsEnabled = !isConnected;
        LogoutAccountButton.IsEnabled = isConnected;
        UpdatePreparationHub(_lastSnapshot);
        UpdatePatchReview(_lastSnapshot);
    }
}
