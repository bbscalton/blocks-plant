using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using BlocksPlant.Desktop.Models;
using BlocksPlant.Desktop.Services;

namespace BlocksPlant.Desktop.Views;

public partial class PosView : UserControl
{
    private readonly ObservableCollection<PosLineItem> _lines = new();
    private List<ProductDto> _products = new();
    private List<CustomerDto> _customers = new();

    public PosView()
    {
        InitializeComponent();
        LinesGrid.ItemsSource = _lines;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _products = await Session.Api!.GetProductsAsync();
            ProductCombo.ItemsSource = _products;
            if (_products.Count > 0) ProductCombo.SelectedIndex = 0;

            _customers = await Session.Api.GetCustomersAsync();
            RefreshCustomers();
            Recalc();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void RefreshCustomers()
    {
        var list = new List<CustomerDto> { new() { Id = 0, Name = "(Walk-in / none)" } };
        list.AddRange(_customers);
        CustomerCombo.ItemsSource = list;
        CustomerCombo.SelectedIndex = 0;
    }

    private void AddLine_Click(object sender, RoutedEventArgs e)
    {
        if (ProductCombo.SelectedItem is not ProductDto product)
        {
            StatusText.Text = "Select a product.";
            return;
        }

        if (!int.TryParse(QtyBox.Text, out var qty) || qty <= 0)
        {
            StatusText.Text = "Enter a valid quantity.";
            return;
        }

        if (product.PricePerBlock is null)
        {
            StatusText.Text = "Price unavailable for this user.";
            return;
        }

        var existing = _lines.FirstOrDefault(l => l.ProductId == product.Id);
        if (existing is not null)
            existing.Quantity += qty;
        else
            _lines.Add(new PosLineItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = qty,
                UnitPrice = product.PricePerBlock.Value
            });

        LinesGrid.Items.Refresh();
        Recalc();
        StatusText.Text = string.Empty;
    }

    private void Recalc()
    {
        var total = _lines.Sum(l => l.LineTotal);
        GrandTotalText.Text = $"Total: {total:C}";
        if (PayFullRadio.IsChecked == true)
            AmountPaidBox.Text = total.ToString("0.00");
        else if (PayCreditRadio.IsChecked == true)
            AmountPaidBox.Text = "0.00";
    }

    private void Fulfillment_Changed(object sender, RoutedEventArgs e)
    {
        if (DeliveryAddressBox is null) return;
        var deliver = DeliverRadio.IsChecked == true;
        DeliveryAddressBox.Visibility = deliver ? Visibility.Visible : Visibility.Collapsed;
        DeliveryNotesBox.Visibility = deliver ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Payment_Changed(object sender, RoutedEventArgs e)
    {
        if (AmountPaidBox is null) return;
        AmountPaidBox.IsEnabled = PayPartialRadio.IsChecked == true;
        Recalc();
    }

    private async void NewCustomer_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new NewCustomerDialog { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true && dlg.Created is not null)
        {
            _customers = await Session.Api!.GetCustomersAsync();
            RefreshCustomers();
            CustomerCombo.SelectedItem = _customers.FirstOrDefault(c => c.Id == dlg.Created.Id)
                                         ?? CustomerCombo.Items.Cast<CustomerDto>().FirstOrDefault(c => c.Id == dlg.Created.Id);
            // rebind and select
            var list = (List<CustomerDto>)CustomerCombo.ItemsSource!;
            CustomerCombo.SelectedItem = list.FirstOrDefault(c => c.Id == dlg.Created.Id);
        }
    }

    private async void CompleteSale_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        if (_lines.Count == 0)
        {
            StatusText.Text = "Add at least one line.";
            return;
        }

        var total = _lines.Sum(l => l.LineTotal);
        if (!decimal.TryParse(AmountPaidBox.Text, out var amountPaid))
        {
            StatusText.Text = "Invalid amount paid.";
            return;
        }

        if (PayFullRadio.IsChecked == true) amountPaid = total;
        if (PayCreditRadio.IsChecked == true) amountPaid = 0;

        int? customerId = null;
        if (CustomerCombo.SelectedItem is CustomerDto c && c.Id > 0)
            customerId = c.Id;

        if (amountPaid < total && customerId is null)
        {
            StatusText.Text = "Customer is required for partial or credit sales.";
            return;
        }

        var deliver = DeliverRadio.IsChecked == true;
        if (deliver && string.IsNullOrWhiteSpace(DeliveryAddressBox.Text))
        {
            StatusText.Text = "Delivery address is required.";
            return;
        }

        try
        {
            var sale = await Session.Api!.CreateSaleAsync(new
            {
                clientId = Guid.NewGuid(),
                customerId,
                fulfillmentType = deliver ? "Deliver" : "Collect",
                deliveryAddress = deliver ? DeliveryAddressBox.Text.Trim() : null,
                deliveryNotes = deliver ? DeliveryNotesBox.Text.Trim() : null,
                amountPaid,
                lines = _lines.Select(l => new { productId = l.ProductId, quantity = l.Quantity }).ToList()
            });

            StatusText.Text = $"Sale #{sale.Id} saved. Total {sale.Subtotal:C}, paid {sale.AmountPaid:C}, due {sale.BalanceDue:C}.";

            var print = MessageBox.Show("Sale completed. Print receipt?", "Print",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (print == MessageBoxResult.Yes)
                ReceiptPrinter.Print(sale);

            ClearForm();
            _products = await Session.Api.GetProductsAsync();
            ProductCombo.ItemsSource = _products;
            if (_products.Count > 0) ProductCombo.SelectedIndex = 0;
            _customers = await Session.Api.GetCustomersAsync();
            RefreshCustomers();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => ClearForm();

    private void ClearForm()
    {
        _lines.Clear();
        QtyBox.Text = "1";
        CollectRadio.IsChecked = true;
        PayFullRadio.IsChecked = true;
        DeliveryAddressBox.Text = string.Empty;
        DeliveryNotesBox.Text = string.Empty;
        Recalc();
    }
}
