using System.Windows;
using System.Windows.Controls;
using BlocksPlant.Desktop.Models;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop.Views;

public partial class StockAdjustView : UserControl
{
    public StockAdjustView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var stock = await Session.Api!.GetStockAsync();
            ProductCombo.ItemsSource = stock;
            if (stock.Count > 0) ProductCombo.SelectedIndex = 0;
            StockGrid.ItemsSource = stock;
            StatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (ProductCombo.SelectedItem is not StockDto product)
        {
            StatusText.Text = "Select a product.";
            return;
        }

        if (!int.TryParse(DeltaBox.Text, out var delta) || delta == 0)
        {
            StatusText.Text = "Enter a non-zero quantity delta.";
            return;
        }

        try
        {
            await Session.Api!.AdjustStockAsync(product.ProductId, delta, ReasonBox.Text.Trim());
            StatusText.Text = "Stock adjusted.";
            DeltaBox.Text = "0";
            ReasonBox.Text = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }
}
