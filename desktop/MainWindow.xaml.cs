using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BlocksPlant.Desktop.Services;
using BlocksPlant.Desktop.Views;

namespace BlocksPlant.Desktop;

public partial class MainWindow : Window
{
    private Button? _activeNav;

    public MainWindow()
    {
        InitializeComponent();
        var user = Session.CurrentUser!;
        UserNameLabel.Text = user.FullName;
        UserRoleLabel.Text = user.Role.ToUpperInvariant();
        BuildNavigation(user.Role);
        Loaded += async (_, _) => await LoadBrandingAsync();
    }

    private async Task LoadBrandingAsync()
    {
        try
        {
            var settings = await Session.Api!.GetSettingsAsync();
            if (!string.IsNullOrWhiteSpace(settings.BusinessName))
                BrandNameLabel.Text = settings.BusinessName.ToUpperInvariant();

            if (!settings.HasLogo) return;
            var bytes = await Session.Api.DownloadLogoBytesAsync();
            if (bytes is null || bytes.Length == 0) return;

            var bitmap = new BitmapImage();
            using var ms = new MemoryStream(bytes);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            SidebarLogo.Source = bitmap;
            SidebarLogo.Visibility = Visibility.Visible;
            DefaultMark.Visibility = Visibility.Collapsed;
            BrandMark.Background = Brushes.Transparent;
        }
        catch
        {
            // keep default branding
        }
    }

    private void BuildNavigation(string role)
    {
        NavPanel.Children.Clear();

        void AddNav(string title, string hint, Action open)
        {
            var btn = new Button
            {
                Content = title,
                Style = (Style)FindResource("NavButton")
            };
            btn.Click += (_, _) =>
            {
                SetActive(btn, title, hint);
                open();
            };
            NavPanel.Children.Add(btn);
        }

        void AddSection(string label)
        {
            NavPanel.Children.Add(new TextBlock
            {
                Text = label.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x7A, 0x8A, 0x93)),
                Margin = new Thickness(12, 14, 0, 6)
            });
        }

        if (role is "Cashier" or "Owner")
        {
            AddSection("Sales floor");
            AddNav("Point of sale", "Build and complete sales", () => ContentHost.Content = new PosView());
            AddNav("Customers", "Balances and debtors", () => ContentHost.Content = new CustomersView());
            AddNav("Sales history", "Recent transactions", () => ContentHost.Content = new SalesHistoryView());
            AddNav("Deliveries", "Pending fulfillment", () => ContentHost.Content = new DeliveriesView());
        }

        if (role is "Operator" or "Owner")
        {
            AddSection("Plant");
            AddNav("Production", "Record good blocks", () => ContentHost.Content = new ProductionView());
        }

        if (role == "Owner")
        {
            AddSection("Owner");
            AddNav("Dashboard", "Operations command center", () => ContentHost.Content = new DashboardView());
            AddNav("Products", "Prices and min stock", () => ContentHost.Content = new ProductsView());
            AddNav("Stock adjust", "Write-offs and corrections", () => ContentHost.Content = new StockAdjustView());
            AddNav("Materials", "Raw inventory", () => ContentHost.Content = new MaterialsView());
            AddNav("Recipes", "BOM per block", () => ContentHost.Content = new RecipesView());
            AddNav("Users", "Staff accounts and roles", () => ContentHost.Content = new UsersView());
            AddNav("Settings", "Store, logo, receipt, backup", () => ContentHost.Content = new SettingsView());
        }

        if (role is "Operator" or "Cashier")
        {
            if (role == "Cashier")
                AddSection("Plant");
            AddNav("Materials", "On-hand raw stock", () => ContentHost.Content = new MaterialsReadView());
        }

        // Default home
        var first = NavPanel.Children.OfType<Button>().FirstOrDefault();
        if (first is not null)
        {
            first.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        else
        {
            ContentHost.Content = role switch
            {
                "Operator" => new ProductionView(),
                _ => new PosView()
            };
        }
    }

    private void SetActive(Button btn, string title, string hint)
    {
        if (_activeNav is not null)
            _activeNav.Style = (Style)FindResource("NavButton");
        _activeNav = btn;
        btn.Style = (Style)FindResource("NavButtonActive");
        PageTitleLabel.Text = title;
        PageHintLabel.Text = hint;
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        Session.Api = null;
        Session.CurrentUser = null;
        var login = new LoginWindow();
        login.Show();
        Close();
    }
}
