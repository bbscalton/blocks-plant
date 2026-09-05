using System.Windows;
using System.Windows.Controls;
using BlocksPlant.Desktop.Models;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop.Views;

public partial class UsersView : UserControl
{
    public UsersView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            Grid.ItemsSource = await Session.Api!.GetUsersAsync();
            StatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Grid.SelectedItem is not UserDto u) return;
        UsernameBox.Text = u.Username;
        UsernameBox.IsEnabled = false;
        FullNameBox.Text = u.FullName;
        ActiveCheck.IsChecked = u.IsActive;
        SelectRole(u.Role);
        PasswordBox.Password = string.Empty;
    }

    private void SelectRole(string role)
    {
        foreach (ComboBoxItem item in RoleBox.Items)
        {
            if (string.Equals(item.Content?.ToString(), role, StringComparison.OrdinalIgnoreCase))
            {
                RoleBox.SelectedItem = item;
                return;
            }
        }
    }

    private string SelectedRole() =>
        (RoleBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cashier";

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        UsernameBox.IsEnabled = true;
        try
        {
            await Session.Api!.CreateUserAsync(
                UsernameBox.Text.Trim(),
                PasswordBox.Password,
                FullNameBox.Text.Trim(),
                SelectedRole());
            StatusText.Text = "User created.";
            ClearForm();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not UserDto user)
        {
            StatusText.Text = "Select a user to edit.";
            return;
        }

        try
        {
            await Session.Api!.UpdateUserAsync(
                user.Id,
                FullNameBox.Text.Trim(),
                SelectedRole(),
                ActiveCheck.IsChecked == true);
            StatusText.Text = "User updated.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void ResetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not UserDto user)
        {
            StatusText.Text = "Select a user.";
            return;
        }

        try
        {
            await Session.Api!.ResetUserPasswordAsync(user.Id, ResetPasswordBox.Password);
            ResetPasswordBox.Password = string.Empty;
            StatusText.Text = "Password reset.";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Deactivate_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not UserDto user)
        {
            StatusText.Text = "Select a user.";
            return;
        }

        try
        {
            await Session.Api!.DeactivateUserAsync(user.Id);
            StatusText.Text = "User deactivated.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void ClearForm()
    {
        UsernameBox.IsEnabled = true;
        UsernameBox.Text = string.Empty;
        FullNameBox.Text = string.Empty;
        PasswordBox.Password = string.Empty;
        ActiveCheck.IsChecked = true;
        RoleBox.SelectedIndex = 1;
        Grid.SelectedItem = null;
    }
}
