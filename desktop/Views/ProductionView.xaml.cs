using System.Windows;
using System.Windows.Controls;
using BlocksPlant.Desktop.Models;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop.Views;

public partial class ProductionView : UserControl
{
    public ProductionView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var products = await Session.Api!.GetProductsAsync();
            ProductCombo.ItemsSource = products;
            if (products.Count > 0) ProductCombo.SelectedIndex = 0;
            StockGrid.ItemsSource = await Session.Api.GetStockAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        if (ProductCombo.SelectedItem is not ProductDto product)
        {
            StatusText.Text = "Select a product.";
            return;
        }

        if (!int.TryParse(QtyBox.Text, out var qty) || qty <= 0)
        {
            StatusText.Text = "Quantity must be greater than zero.";
            return;
        }

        if (!int.TryParse(RejectsBox.Text, out var rejects) || rejects < 0)
            rejects = 0;

        try
        {
            var entry = await Session.Api!.CreateProductionAsync(product.Id, qty, rejects);
            StatusText.Text = $"Recorded {entry.Quantity} of {entry.ProductName} (rejects: {entry.Rejects}). Stock updated.";
            QtyBox.Text = "0";
            RejectsBox.Text = "0";
            StockGrid.ItemsSource = await Session.Api.GetStockAsync();
            ProductCombo.ItemsSource = await Session.Api.GetProductsAsync();
            ProductCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }
}
