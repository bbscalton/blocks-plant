using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BlocksPlant.Desktop.Models;

namespace BlocksPlant.Desktop.Services;

public static class ReceiptPrinter
{
    public static void Print(SaleDto sale)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(40),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12
        };

        doc.Blocks.Add(new Paragraph(new Run("BLOCKS PLANT")) { FontSize = 16, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center });
        doc.Blocks.Add(new Paragraph(new Run("Sales Receipt")) { TextAlignment = TextAlignment.Center });
        doc.Blocks.Add(new Paragraph(new Run($"Sale #: {sale.Id}")));
        doc.Blocks.Add(new Paragraph(new Run($"Date: {sale.CreatedAt.ToLocalTime():g}")));
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
        doc.Blocks.Add(new Paragraph(new Run("Thank you!")) { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 16, 0, 0) });

        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() == true)
        {
            doc.PageWidth = printDialog.PrintableAreaWidth;
            doc.ColumnWidth = printDialog.PrintableAreaWidth;
            IDocumentPaginatorSource source = doc;
            printDialog.PrintDocument(source.DocumentPaginator, $"Sale {sale.Id}");
        }
    }
}
