using System.Windows;
using System.Windows.Controls;
using BlocksPlant.Desktop.Models;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop.Views;

public partial class MaterialsView : UserControl
{
    public MaterialsView()
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

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Grid.SelectedItem is not RawMaterialDto m) return;
        NameBox.Text = m.Name;
        UnitBox.Text = m.Unit;
        MinBox.Text = m.MinStock.ToString("0.####");
        NotesBox.Text = m.Notes ?? string.Empty;
        QtyBox.Text = "0";
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(UnitBox.Text))
        {
            StatusText.Text = "Name and unit are required.";
            return;
        }

        if (!decimal.TryParse(MinBox.Text, out var min) || min < 0 ||
            !decimal.TryParse(QtyBox.Text, out var qty) || qty < 0)
        {
            StatusText.Text = "Enter valid min stock and initial qty.";
            return;
        }

        try
        {
            await Session.Api!.CreateMaterialAsync(
                NameBox.Text.Trim(), UnitBox.Text.Trim(), qty, min,
                string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim());
            StatusText.Text = "Material added.";
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
        if (Grid.SelectedItem is not RawMaterialDto material)
        {
            StatusText.Text = "Select a material to save.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(UnitBox.Text))
        {
            StatusText.Text = "Name and unit are required.";
            return;
        }

        if (!decimal.TryParse(MinBox.Text, out var min) || min < 0)
        {
            StatusText.Text = "Enter a valid min stock.";
            return;
        }

        try
        {
            await Session.Api!.UpdateMaterialAsync(
                material.Id, NameBox.Text.Trim(), UnitBox.Text.Trim(), min,
                string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim());
            StatusText.Text = "Saved.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Receive_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not RawMaterialDto material)
        {
            StatusText.Text = "Select a material.";
            return;
        }

        if (!decimal.TryParse(ReceiveBox.Text, out var qty) || qty <= 0)
        {
            StatusText.Text = "Enter a positive receive quantity.";
            return;
        }

        try
        {
            await Session.Api!.ReceiveMaterialAsync(
                material.Id, qty,
                string.IsNullOrWhiteSpace(ReceiveNotesBox.Text) ? null : ReceiveNotesBox.Text.Trim());
            StatusText.Text = $"Received {qty:0.####} {material.Unit} of {material.Name}.";
            ReceiveBox.Text = "0";
            ReceiveNotesBox.Text = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Adjust_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not RawMaterialDto material)
        {
            StatusText.Text = "Select a material.";
            return;
        }

        if (!decimal.TryParse(AdjustBox.Text, out var delta) || delta == 0)
        {
            StatusText.Text = "Enter a non-zero adjust delta.";
            return;
        }

        try
        {
            await Session.Api!.AdjustMaterialAsync(
                material.Id, delta,
                string.IsNullOrWhiteSpace(AdjustNotesBox.Text) ? null : AdjustNotesBox.Text.Trim());
            StatusText.Text = "Material stock adjusted.";
            AdjustBox.Text = "0";
            AdjustNotesBox.Text = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void ClearForm()
    {
        NameBox.Text = string.Empty;
        UnitBox.Text = string.Empty;
        MinBox.Text = "0";
        QtyBox.Text = "0";
        NotesBox.Text = string.Empty;
    }
}
