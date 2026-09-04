using System.Windows;
using System.Windows.Controls;
using BlocksPlant.Desktop.Services;
using BlocksPlant.Desktop.Views;

namespace BlocksPlant.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var user = Session.CurrentUser!;
        UserLabel.Text = $"{user.FullName} · {user.Role}";
        BuildNavigation(user.Role);
    }

    private void BuildNavigation(string role)
    {
        NavPanel.Children.Clear();

        void AddNav(string title, Action open)
        {
            var btn = new Button
            {
                Content = title,
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(10, 8, 10, 8),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.Transparent
            };
            btn.Click += (_, _) => open();
            NavPanel.Children.Add(btn);
        }

        if (role is "Cashier" or "Owner")
        {
            AddNav("POS", () => ContentHost.Content = new PosView());
            AddNav("Customers", () => ContentHost.Content = new CustomersView());
            AddNav("Sales history", () => ContentHost.Content = new SalesHistoryView());
            AddNav("Deliveries", () => ContentHost.Content = new DeliveriesView());
        }

        if (role is "Operator" or "Owner")
            AddNav("Production", () => ContentHost.Content = new ProductionView());

        if (role == "Owner")
        {
            AddNav("Dashboard", () => ContentHost.Content = new DashboardView());
            AddNav("Products", () => ContentHost.Content = new ProductsView());
            AddNav("Stock adjust", () => ContentHost.Content = new StockAdjustView());
            AddNav("Materials", () => ContentHost.Content = new MaterialsView());
            AddNav("Recipes", () => ContentHost.Content = new RecipesView());
        }

        if (role is "Operator" or "Cashier")
            AddNav("Materials", () => ContentHost.Content = new MaterialsReadView());


        // Default home
        ContentHost.Content = role switch
        {
            "Operator" => new ProductionView(),
            "Owner" => new PosView(),
            _ => new PosView()
        };
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
