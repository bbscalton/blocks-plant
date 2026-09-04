using System.Windows;
using BlocksPlant.Desktop.Models;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop.Views;

public partial class PaymentDialog : Window
{
    private readonly CustomerDto _customer;

    public PaymentDialog(CustomerDto customer)
    {
        InitializeComponent();
        _customer = customer;
        InfoText.Text = $"{customer.Name}\nOutstanding balance: {customer.Balance:C}";
        AmountBox.Text = customer.Balance.ToString("0.00");
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        if (!decimal.TryParse(AmountBox.Text, out var amount) || amount <= 0)
        {
            ErrorText.Text = "Enter a valid amount.";
            return;
        }

        try
        {
            await Session.Api!.RecordPaymentAsync(_customer.Id, null, amount);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
