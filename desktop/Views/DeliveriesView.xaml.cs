using System.Windows;
using System.Windows.Controls;
using BlocksPlant.Desktop.Models;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop.Views;

public partial class DeliveriesView : UserControl
{
    public DeliveriesView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            Grid.ItemsSource = await Session.Api!.GetDeliveriesAsync();
            StatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void MarkDelivered_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not DeliveryDto delivery)
        {
            StatusText.Text = "Select a delivery.";
            return;
        }

        try
        {
            await Session.Api!.UpdateDeliveryStatusAsync(delivery.SaleId, "Delivered");
            StatusText.Text = $"Sale #{delivery.SaleId} marked delivered.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }
}
