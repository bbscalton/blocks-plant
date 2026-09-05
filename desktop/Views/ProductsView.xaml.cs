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

    private async void Reload_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        try
        {
            Grid.ItemsSource = await Session.Api!.GetProductsAsync(ShowInactiveCheck.IsChecked == true);
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

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not ProductDto product)
        {
            StatusText.Text = "Select a product.";
            return;
        }

        var confirm = MessageBox.Show(
            $"Remove {product.Name}? Unused products are deleted; products with sales history are deactivated.",
            "Delete / deactivate",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var result = await Session.Api!.DeleteProductAsync(product.Id);
            StatusText.Text = result.Message;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Activate_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not ProductDto product)
        {
            StatusText.Text = "Select a product.";
            return;
        }

        try
        {
            await Session.Api!.ActivateProductAsync(product.Id);
            StatusText.Text = "Product reactivated.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewNameBox.Text) ||
            !int.TryParse(NewSizeBox.Text, out var size) || size <= 0 ||
            !decimal.TryParse(NewPriceBox.Text, out var price) || price < 0 ||
            !int.TryParse(NewMinBox.Text, out var min) || min < 0)
        {
            StatusText.Text = "Enter name, size, price, and min stock.";
            return;
        }

        try
        {
            await Session.Api!.CreateProductAsync(NewNameBox.Text.Trim(), size, price, min);
            StatusText.Text = "Product added.";
            NewNameBox.Text = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }
}
