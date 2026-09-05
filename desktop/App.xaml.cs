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

        void Report(string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (MainWindow is LoginWindow login)
                    login.SetHostStatus(message);
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
}
