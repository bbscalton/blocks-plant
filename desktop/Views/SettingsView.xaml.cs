using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using BlocksPlant.Desktop.Models;
using BlocksPlant.Desktop.Services;
using Microsoft.Win32;

namespace BlocksPlant.Desktop.Views;

public partial class SettingsView : UserControl
{
    private PlantSettingsDto _settings = new();

    public SettingsView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
        NameBox.TextChanged += (_, _) => UpdatePreview();
        AddressBox.TextChanged += (_, _) => UpdatePreview();
        PhoneBox.TextChanged += (_, _) => UpdatePreview();
        TaxIdBox.TextChanged += (_, _) => UpdatePreview();
        FooterBox.TextChanged += (_, _) => UpdatePreview();
        ShowLogoCheck.Checked += (_, _) => UpdatePreview();
        ShowLogoCheck.Unchecked += (_, _) => UpdatePreview();
        ShowNameCheck.Checked += (_, _) => UpdatePreview();
        ShowNameCheck.Unchecked += (_, _) => UpdatePreview();
        ShowAddressCheck.Checked += (_, _) => UpdatePreview();
        ShowAddressCheck.Unchecked += (_, _) => UpdatePreview();
        ShowPhoneCheck.Checked += (_, _) => UpdatePreview();
        ShowPhoneCheck.Unchecked += (_, _) => UpdatePreview();
        ShowCashierCheck.Checked += (_, _) => UpdatePreview();
        ShowCashierCheck.Unchecked += (_, _) => UpdatePreview();
        ShowThankYouCheck.Checked += (_, _) => UpdatePreview();
        ShowThankYouCheck.Unchecked += (_, _) => UpdatePreview();
    }

    private async Task LoadAsync()
    {
        try
        {
            _settings = await Session.Api!.GetSettingsAsync();
            BindForm();
            await LoadLogoPreviewAsync();
            UpdatePreview();
            StatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void BindForm()
    {
        NameBox.Text = _settings.BusinessName;
        AddressBox.Text = _settings.Address ?? string.Empty;
        PhoneBox.Text = _settings.Phone ?? string.Empty;
        EmailBox.Text = _settings.Email ?? string.Empty;
        TaxIdBox.Text = _settings.TaxId ?? string.Empty;
        FooterBox.Text = _settings.ReceiptFooter ?? "Thank you!";
        ShowLogoCheck.IsChecked = _settings.ReceiptShowLogo;
        ShowNameCheck.IsChecked = _settings.ReceiptShowStoreName;
        ShowAddressCheck.IsChecked = _settings.ReceiptShowAddress;
        ShowPhoneCheck.IsChecked = _settings.ReceiptShowPhone;
        ShowCashierCheck.IsChecked = _settings.ReceiptShowCashier;
        ShowThankYouCheck.IsChecked = _settings.ReceiptShowThankYou;
    }

    private object BuildBody() => new
    {
        businessName = NameBox.Text.Trim(),
        address = NullIfBlank(AddressBox.Text),
        phone = NullIfBlank(PhoneBox.Text),
        email = NullIfBlank(EmailBox.Text),
        taxId = NullIfBlank(TaxIdBox.Text),
        receiptFooter = NullIfBlank(FooterBox.Text) ?? "Thank you!",
        receiptShowLogo = ShowLogoCheck.IsChecked == true,
        receiptShowStoreName = ShowNameCheck.IsChecked == true,
        receiptShowAddress = ShowAddressCheck.IsChecked == true,
        receiptShowPhone = ShowPhoneCheck.IsChecked == true,
        receiptShowCashier = ShowCashierCheck.IsChecked == true,
        receiptShowThankYou = ShowThankYouCheck.IsChecked == true
    };

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings = await Session.Api!.UpdateSettingsAsync(BuildBody());
            BindForm();
            UpdatePreview();
            StatusText.Text = "Settings saved.";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void UploadLogo_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.webp|All files|*.*",
            Title = "Choose plant logo"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _settings = await Session.Api!.UploadLogoAsync(dlg.FileName);
            await LoadLogoPreviewAsync();
            UpdatePreview();
            StatusText.Text = "Logo uploaded.";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void ClearLogo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await Session.Api!.ClearLogoAsync();
            _settings.HasLogo = false;
            LogoPreview.Source = null;
            UpdatePreview();
            StatusText.Text = "Logo removed.";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "SQLite database|*.db",
            FileName = $"blocksplant-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db",
            Title = "Save database backup"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            await Session.Api!.DownloadBackupAsync(dlg.FileName);
            StatusText.Text = $"Backup saved to {dlg.FileName}";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Restore will OVERWRITE the live database. A pre-restore copy is kept on the server. Continue?",
            "Restore backup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var dlg = new OpenFileDialog
        {
            Filter = "SQLite database|*.db",
            Title = "Choose backup to restore"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            await Session.Api!.RestoreBackupAsync(dlg.FileName);
            StatusText.Text = "Database restored. Restart the API if data looks stale.";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ChangePasswordDialog { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
    }

    private async Task LoadLogoPreviewAsync()
    {
        LogoPreview.Source = null;
        if (!_settings.HasLogo) return;
        try
        {
            var bytes = await Session.Api!.DownloadLogoBytesAsync();
            if (bytes is null || bytes.Length == 0) return;
            var bitmap = new BitmapImage();
            using var ms = new MemoryStream(bytes);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            LogoPreview.Source = bitmap;
        }
        catch
        {
            LogoPreview.Source = null;
        }
    }

    private void UpdatePreview()
    {
        var sb = new StringBuilder();
        if (ShowLogoCheck.IsChecked == true && _settings.HasLogo)
            sb.AppendLine("[logo]");
        if (ShowNameCheck.IsChecked == true)
            sb.AppendLine(string.IsNullOrWhiteSpace(NameBox.Text) ? "BLOCKS PLANT" : NameBox.Text.ToUpperInvariant());
        sb.AppendLine("Sales Receipt");
        if (ShowAddressCheck.IsChecked == true && !string.IsNullOrWhiteSpace(AddressBox.Text))
            sb.AppendLine(AddressBox.Text.Trim());
        if (ShowPhoneCheck.IsChecked == true && !string.IsNullOrWhiteSpace(PhoneBox.Text))
            sb.AppendLine(PhoneBox.Text.Trim());
        if (!string.IsNullOrWhiteSpace(TaxIdBox.Text))
            sb.AppendLine($"Tax ID: {TaxIdBox.Text.Trim()}");
        sb.AppendLine("Sale #: 123");
        sb.AppendLine($"Date: {DateTime.Now:g}");
        if (ShowCashierCheck.IsChecked == true)
            sb.AppendLine("Cashier: Sample Cashier");
        sb.AppendLine("--------------------------------");
        sb.AppendLine("4 Inch Block  x10 @ $2.50 = $25.00");
        sb.AppendLine("--------------------------------");
        sb.AppendLine("Subtotal:   $25.00");
        sb.AppendLine("Paid:       $25.00");
        sb.AppendLine("Balance Due:$0.00");
        if (ShowThankYouCheck.IsChecked == true)
            sb.AppendLine(string.IsNullOrWhiteSpace(FooterBox.Text) ? "Thank you!" : FooterBox.Text.Trim());
        PreviewText.Text = sb.ToString();
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
