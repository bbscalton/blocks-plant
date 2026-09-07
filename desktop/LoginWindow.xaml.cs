using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        ApiUrlBox.Text = Session.Config.ApiBaseUrl;
        PasswordBox.Password = "cashier123";
        UsernameBox.Focus();

        if (Session.Config.AutoStartApi || Session.Config.AutoStartWeb)
        {
            HostStatusText.Text = string.IsNullOrWhiteSpace(LocalHostLauncher.Instance.LastStatus)
                ? "Starting local API / web…"
                : LocalHostLauncher.Instance.LastStatus;
        }

        Loaded += async (_, _) =>
        {
            // Pick up any status that arrived before this window existed.
            if (!string.IsNullOrWhiteSpace(LocalHostLauncher.Instance.LastStatus))
                HostStatusText.Text = LocalHostLauncher.Instance.LastStatus!;
            else if (!string.IsNullOrWhiteSpace(LocalHostLauncher.Instance.LastError))
                HostStatusText.Text = LocalHostLauncher.Instance.LastError!;

            await TryLoadBrandingAsync();
        };
    }

    public void SetHostStatus(string message)
    {
        HostStatusText.Text = message ?? string.Empty;
    }

    private async Task TryLoadBrandingAsync()
    {
        try
        {
            var api = new ApiClient(Session.Config);
            var settings = await api.GetSettingsAsync();
            if (!string.IsNullOrWhiteSpace(settings.BusinessName))
                LoginBrandName.Text = settings.BusinessName.ToUpperInvariant();

            if (!settings.HasLogo) return;
            var bytes = await api.DownloadLogoBytesAsync();
            if (bytes is null || bytes.Length == 0) return;

            var bitmap = new BitmapImage();
            using var ms = new MemoryStream(bytes);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            LoginLogo.Source = bitmap;
            LoginLogo.Visibility = Visibility.Visible;
            LoginDefaultMark.Visibility = Visibility.Collapsed;
            LoginBrandMark.Background = Brushes.Transparent;
        }
        catch
        {
            // API may not be up yet — keep defaults
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        LoginButton.IsEnabled = false;

        try
        {
            Session.Config.ApiBaseUrl = ApiUrlBox.Text.Trim();
            Session.Config.Save();

            var api = new ApiClient(Session.Config);
            var login = await api.LoginAsync(UsernameBox.Text.Trim(), PasswordBox.Password);
            api.SetToken(login.Token);
            Session.Api = api;
            Session.CurrentUser = login;

            var main = new MainWindow();
            main.Show();
            Close();
        }
        catch (ApiException ex)
        {
            ErrorText.Text = ex.Message;
        }
        catch (Exception ex)
        {
            var hint = LocalHostLauncher.Instance.LastError;
            ErrorText.Text = string.IsNullOrEmpty(hint)
                ? $"Cannot reach API. Check the base URL and that the server is running.\n{ex.Message}"
                : $"Cannot reach API. {hint}\n{ex.Message}";
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }
}
