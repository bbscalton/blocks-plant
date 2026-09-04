using System.Windows;
using System.Windows.Controls;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var d = await Session.Api!.GetDashboardAsync();
            ProdText.Text = d.TodayProductionQty.ToString("N0");
            SalesText.Text = d.TodaySalesTotal.ToString("C");
            CountText.Text = d.TodaySalesCount.ToString();
            UnpaidText.Text = $"{d.TotalUnpaid:C} ({d.DebtorCount} debtors)";
            StockGrid.ItemsSource = d.Stock;
            MaterialsGrid.ItemsSource = d.Materials;
            StatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();
}
