using System.Windows;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Kick off API + web in the background; do not block the login UI.
        // Started hosts are left running when Desktop exits.
        _ = EnsureLocalHostsAsync();
    }

    private async Task EnsureLocalHostsAsync()
    {
        var config = Session.Config;
        if (!config.AutoStartApi && !config.AutoStartWeb)
            return;

        // StartupUri creates LoginWindow after OnStartup returns — wait briefly so
        // status updates land on the login form instead of being dropped.
        for (var i = 0; i < 50 && FindLoginWindow() is null; i++)
            await Task.Delay(50);

        void Report(string message)
        {
            Dispatcher.Invoke(() =>
            {
                var login = FindLoginWindow();
                login?.SetHostStatus(message);
            });
        }

        try
        {
            await LocalHostLauncher.Instance.EnsureAllAsync(config, Report);
        }
        catch (Exception ex)
        {
            Report($"Could not auto-start local hosts: {ex.Message}");
        }
    }

    private static LoginWindow? FindLoginWindow()
    {
        if (Current?.MainWindow is LoginWindow main)
            return main;

        if (Current?.Windows is null)
            return null;

        foreach (Window window in Current.Windows)
        {
            if (window is LoginWindow login)
                return login;
        }

        return null;
    }
}
