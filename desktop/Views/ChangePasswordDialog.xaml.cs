using System.Windows;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop.Views;

public partial class ChangePasswordDialog : Window
{
    public ChangePasswordDialog()
    {
        InitializeComponent();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        if (NewBox.Password != ConfirmBox.Password)
        {
            ErrorText.Text = "New passwords do not match.";
            return;
        }

        try
        {
            await Session.Api!.ChangePasswordAsync(CurrentBox.Password, NewBox.Password);
            MessageBox.Show("Password changed.", "Blocks Plant", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
