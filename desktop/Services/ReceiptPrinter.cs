using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BlocksPlant.Desktop.Models;

namespace BlocksPlant.Desktop.Services;

public static class ReceiptPrinter
{
    public static async Task PrintAsync(SaleDto sale, PlantSettingsDto? settings = null, byte[]? logoBytes = null)
    {
        settings ??= new PlantSettingsDto();

        var doc = new FlowDocument
        {
            PagePadding = new Thickness(40),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12
        };

        if (settings.ReceiptShowLogo && logoBytes is { Length: > 0 })
        {
            try
            {
                var image = new Image { Width = 96, Stretch = Stretch.Uniform };
                var bitmap = new BitmapImage();
                using var ms = new MemoryStream(logoBytes);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                image.Source = bitmap;
                var block = new BlockUIContainer(image) { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
                doc.Blocks.Add(block);
            }
            catch
            {
                // skip broken logo
            }
        }

        if (settings.ReceiptShowStoreName)
        {
            var name = string.IsNullOrWhiteSpace(settings.BusinessName) ? "BLOCKS PLANT" : settings.BusinessName.ToUpperInvariant();
            doc.Blocks.Add(new Paragraph(new Run(name)) { FontSize = 16, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center });
        }

        doc.Blocks.Add(new Paragraph(new Run("Sales Receipt")) { TextAlignment = TextAlignment.Center });

        if (settings.ReceiptShowAddress && !string.IsNullOrWhiteSpace(settings.Address))
            doc.Blocks.Add(new Paragraph(new Run(settings.Address)) { TextAlignment = TextAlignment.Center });
        if (settings.ReceiptShowPhone && !string.IsNullOrWhiteSpace(settings.Phone))
            doc.Blocks.Add(new Paragraph(new Run(settings.Phone)) { TextAlignment = TextAlignment.Center });
        if (!string.IsNullOrWhiteSpace(settings.TaxId))
            doc.Blocks.Add(new Paragraph(new Run($"Tax ID: {settings.TaxId}")) { TextAlignment = TextAlignment.Center });

        doc.Blocks.Add(new Paragraph(new Run($"Sale #: {sale.Id}")));
        doc.Blocks.Add(new Paragraph(new Run($"Date: {sale.CreatedAt.ToLocalTime():g}")));
        if (settings.ReceiptShowCashier)
            doc.Blocks.Add(new Paragraph(new Run($"Cashier: {sale.CashierName}")));
        if (!string.IsNullOrWhiteSpace(sale.CustomerName))
            doc.Blocks.Add(new Paragraph(new Run($"Customer: {sale.CustomerName}")));
        doc.Blocks.Add(new Paragraph(new Run($"Fulfillment: {sale.FulfillmentType}")));
        if (!string.IsNullOrWhiteSpace(sale.DeliveryAddress))
            doc.Blocks.Add(new Paragraph(new Run($"Deliver to: {sale.DeliveryAddress}")));

        doc.Blocks.Add(new Paragraph(new Run("----------------------------------------")));

        foreach (var line in sale.Lines)
        {
            doc.Blocks.Add(new Paragraph(new Run(
                $"{line.ProductName}  x{line.Quantity} @ {line.UnitPrice:C} = {line.LineTotal:C}")));
        }

        doc.Blocks.Add(new Paragraph(new Run("----------------------------------------")));
        doc.Blocks.Add(new Paragraph(new Run($"Subtotal:   {sale.Subtotal:C}")));
        doc.Blocks.Add(new Paragraph(new Run($"Paid:       {sale.AmountPaid:C}")));
        doc.Blocks.Add(new Paragraph(new Run($"Balance Due:{sale.BalanceDue:C}")) { FontWeight = FontWeights.Bold });

        if (settings.ReceiptShowThankYou)
        {
            var footer = string.IsNullOrWhiteSpace(settings.ReceiptFooter) ? "Thank you!" : settings.ReceiptFooter;
            doc.Blocks.Add(new Paragraph(new Run(footer)) { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 16, 0, 0) });
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                doc.PageWidth = printDialog.PrintableAreaWidth;
                doc.ColumnWidth = printDialog.PrintableAreaWidth;
                IDocumentPaginatorSource source = doc;
                printDialog.PrintDocument(source.DocumentPaginator, $"Sale {sale.Id}");
            }
        });
    }

    /// <summary>Sync wrapper used by existing call sites.</summary>
    public static void Print(SaleDto sale)
    {
        PlantSettingsDto? settings = null;
        byte[]? logo = null;
        try
        {
            if (Session.Api is not null)
            {
                settings = Session.Api.GetSettingsAsync().GetAwaiter().GetResult();
                if (settings.HasLogo)
                    logo = Session.Api.DownloadLogoBytesAsync().GetAwaiter().GetResult();
            }
        }
        catch
        {
            settings = new PlantSettingsDto();
        }

        PrintAsync(sale, settings, logo).GetAwaiter().GetResult();
    }
}
