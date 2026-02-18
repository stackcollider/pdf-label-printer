using Microsoft.VisualBasic;
using System.Drawing.Printing;
using System.Runtime.Versioning;

namespace PdfPrinterTool;

[SupportedOSPlatform("windows6.1")]
internal static class PrinterHelper
{
    public static IReadOnlyList<string> GetInstalledPrinters()
    {
        var result = new List<string>();
        foreach (string name in PrinterSettings.InstalledPrinters)
            result.Add(name);
        return result;
    }

    public static void PrintInfo(string printerName)
    {
        var settings = new PrinterSettings { PrinterName = printerName };

        Console.WriteLine($"Printer: {printerName}");
        Console.WriteLine($"   Status: {(settings.IsValid ? "Available" : "Unavailable")}");
        Console.WriteLine($"   Default printer: {(settings.IsDefaultPrinter ? "Yes" : "No")}");

        if (!settings.IsValid)
            return;

        var paper = settings.DefaultPageSettings.PaperSize;
        Console.WriteLine($"\n   Default paper size:");
        Console.WriteLine($"      Name: {paper.PaperName}");
        Console.WriteLine($"      Size (1/100 inch): {paper.Width} x {paper.Height}");

        double widthMm = paper.Width * PdfPrintConstants.HundredthInchToMm;
        double heightMm = paper.Height * PdfPrintConstants.HundredthInchToMm;
        double widthPt = widthMm * PdfPrintConstants.MmToPt;
        double heightPt = heightMm * PdfPrintConstants.MmToPt;
        double widthIn = paper.Width / 100.0;
        double heightIn = paper.Height / 100.0;

        Console.WriteLine($"      Points: {widthPt:F1} x {heightPt:F1} pt");
        Console.WriteLine($"      MM: {widthMm:F1} x {heightMm:F1} mm");
        Console.WriteLine($"      Inches: {widthIn:F2} x {heightIn:F2} inch");

        Console.WriteLine($"\n   Supported paper sizes ({settings.PaperSizes.Count}):");
        foreach (PaperSize size in settings.PaperSizes)
        {
            double wMm = size.Width * PdfPrintConstants.HundredthInchToMm;
            double hMm = size.Height * PdfPrintConstants.HundredthInchToMm;
            Console.WriteLine($"      {size.PaperName,-20} ({size.Kind,-15}) - {wMm:F0}x{hMm:F0} mm");
        }

        Console.WriteLine($"\n   Additional info:");
        Console.WriteLine($"      Color support: {(settings.SupportsColor ? "Yes" : "No")}");
        Console.WriteLine($"      Duplex: {settings.Duplex}");
        Console.WriteLine($"      Max copies: {settings.MaximumCopies}");
        Console.WriteLine($"      Orientation: {(settings.DefaultPageSettings.Landscape ? "Landscape" : "Portrait")}");
        Console.WriteLine();
    }

    public static (double widthMm, double heightMm) GetPaperSizeMm(string printerName)
    {
        var settings = new PrinterSettings { PrinterName = printerName };

        if (!settings.IsValid)
            throw new InvalidOperationException($"Printer '{printerName}' not found or unavailable");

        var paper = settings.DefaultPageSettings.PaperSize;
        return (
            paper.Width * PdfPrintConstants.HundredthInchToMm,
            paper.Height * PdfPrintConstants.HundredthInchToMm
        );
    }
}
