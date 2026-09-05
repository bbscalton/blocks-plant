using System.Windows;
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
            HostStatusText.Text = "Checking local API / web…";
    }

    public void SetHostStatus(string message)
    {
        HostStatusText.Text = message ?? string.Empty;
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
