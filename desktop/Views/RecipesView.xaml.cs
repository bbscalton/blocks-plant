using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using BlocksPlant.Desktop.Models;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop.Views;

public partial class RecipesView : UserControl
{
    private readonly ObservableCollection<RecipeEditRow> _rows = new();
    private List<ProductRecipeDto> _recipes = new();

    public RecipesView()
    {
        InitializeComponent();
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _recipes = await Session.Api!.GetRecipesAsync();
            ProductCombo.ItemsSource = _recipes;
            if (_recipes.Count > 0 && ProductCombo.SelectedIndex < 0)
                ProductCombo.SelectedIndex = 0;
            else
                BindSelected();
            StatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void ProductCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => BindSelected();

    private async void Reload_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void BindSelected()
    {
        _rows.Clear();
        if (ProductCombo.SelectedItem is not ProductRecipeDto recipe) return;

        try
        {
            var materials = await Session.Api!.GetMaterialsAsync();
            var byId = recipe.Lines.ToDictionary(l => l.RawMaterialId);
            foreach (var m in materials.OrderBy(x => x.Name))
            {
                byId.TryGetValue(m.Id, out var line);
                _rows.Add(new RecipeEditRow
                {
                    RawMaterialId = m.Id,
                    MaterialName = m.Name,
                    Unit = m.Unit,
                    QuantityPerBlock = line?.QuantityPerBlock ?? 0
                });
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ProductCombo.SelectedItem is not ProductRecipeDto recipe)
        {
            StatusText.Text = "Select a product.";
            return;
        }

        var lines = _rows
            .Where(r => r.QuantityPerBlock > 0)
            .Select(r => (object)new { rawMaterialId = r.RawMaterialId, quantityPerBlock = r.QuantityPerBlock })
            .ToList();

        try
        {
            var saved = await Session.Api!.SetRecipeAsync(recipe.ProductId, lines);
            StatusText.Text = $"Saved recipe for {saved.ProductName} ({saved.Lines.Count} materials).";
            await LoadAsync();
            var idx = _recipes.FindIndex(r => r.ProductId == recipe.ProductId);
            if (idx >= 0) ProductCombo.SelectedIndex = idx;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private sealed class RecipeEditRow : INotifyPropertyChanged
    {
        private decimal _qty;
        public int RawMaterialId { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;

        public decimal QuantityPerBlock
        {
            get => _qty;
            set
            {
                if (_qty == value) return;
                _qty = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
