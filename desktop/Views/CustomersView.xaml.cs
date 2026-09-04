using System.Windows;
using System.Windows.Controls;
using BlocksPlant.Desktop.Models;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop.Views;

public partial class CustomersView : UserControl
{
    public CustomersView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync(string? search = null)
    {
        try
        {
            Grid.ItemsSource = await Session.Api!.GetCustomersAsync(search);
            StatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await LoadAsync(SearchBox.Text);

    private async void Debtors_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Grid.ItemsSource = await Session.Api!.GetDebtorsAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void New_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new NewCustomerDialog { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
            await LoadAsync();
    }

    private async void Payment_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not CustomerDto customer)
        {
            StatusText.Text = "Select a customer first.";
            return;
        }

        if (customer.Balance <= 0)
        {
            StatusText.Text = "Customer has no balance due.";
            return;
        }

        var dlg = new PaymentDialog(customer) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
            await LoadAsync(SearchBox.Text);
    }
}
