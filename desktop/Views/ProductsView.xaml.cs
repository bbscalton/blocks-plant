using System.Windows;
using System.Windows.Controls;
using BlocksPlant.Desktop.Models;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop.Views;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            Grid.ItemsSource = await Session.Api!.GetProductsAsync();
            StatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Grid.SelectedItem is ProductDto p)
        {
            PriceBox.Text = (p.PricePerBlock ?? 0).ToString("0.00");
            MinStockBox.Text = p.MinStock.ToString();
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not ProductDto product)
        {
            StatusText.Text = "Select a product.";
            return;
        }

        if (!decimal.TryParse(PriceBox.Text, out var price) || price < 0 ||
            !int.TryParse(MinStockBox.Text, out var min) || min < 0)
        {
            StatusText.Text = "Enter valid price and min stock.";
            return;
        }

        try
        {
            await Session.Api!.UpdateProductAsync(product.Id, price, min);
            StatusText.Text = "Saved.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }
}
