using System.Windows;
using BlocksPlant.Desktop.Models;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop.Views;

public partial class NewCustomerDialog : Window
{
    public CustomerDto? Created { get; private set; }

    public NewCustomerDialog()
    {
        InitializeComponent();
        NameBox.Focus();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = "Name is required.";
            return;
        }

        try
        {
            Created = await Session.Api!.CreateCustomerAsync(NameBox.Text.Trim(), PhoneBox.Text.Trim(), AddressBox.Text.Trim());
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
