using Microsoft.VisualBasic;
using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace PdfPrinterTool;

internal static class TestPdfGenerator
{
    public static byte[] Generate(string printerName, string? clientIp,
                                  double widthMm = 210, double heightMm = 297)
    {
        using var doc = new PdfSharp.Pdf.PdfDocument();
        var page = doc.AddPage();

        page.Width = XUnit.FromPoint(widthMm * PdfPrintConstants.MmToPt);
        page.Height = XUnit.FromPoint(heightMm * PdfPrintConstants.MmToPt);

        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        var fontTitle = new XFont("Arial", 10, XFontStyleEx.Regular);
        var fontNormal = new XFont("Arial", 12, XFontStyleEx.Regular);
        var border = new XPen(XColors.Black, 2);

        double w = page.Width.Point;
        double h = page.Height.Point;

        using var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawRectangle(border, 20, 20, w - 40, h - 40);
        gfx.DrawString("TEST PAGE", fontTitle, XBrushes.Black,
            new XRect(0, 80, w, 50), XStringFormats.Center);
        gfx.DrawString($"Printer: {printerName}", fontNormal, XBrushes.Black,
            new XRect(50, 160, w - 100, 25), XStringFormats.TopLeft);
        gfx.DrawString($"IP: {clientIp ?? "unknown"}", fontNormal, XBrushes.Black,
            new XRect(50, 190, w - 100, 25), XStringFormats.TopLeft);
        gfx.DrawString($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", fontNormal, XBrushes.Black,
            new XRect(50, 220, w - 100, 25), XStringFormats.TopLeft);

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }
}