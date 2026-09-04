using System.Windows;
using System.Windows.Controls;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop.Views;

public partial class MaterialsReadView : UserControl
{
    public MaterialsReadView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            Grid.ItemsSource = await Session.Api!.GetMaterialsAsync();
            StatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();
}
