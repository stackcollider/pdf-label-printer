using Microsoft.VisualBasic;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Diagnostics;
using UglyToad.PdfPig.Content;

namespace PdfPrinterTool;

internal static class PdfProcessor
{
    // ─────────────────────────────────────────────────────────────────────────
    // Crop + Scale PDF под размер бумаги принтера
    // ─────────────────────────────────────────────────────────────────────────
    public static string CropAndScalePdf(string inputPath, double targetWidthMm, double targetHeightMm)
    {
        var outputDir = Path.GetDirectoryName(inputPath)!;
        var fileName = Path.GetFileNameWithoutExtension(inputPath);
        var outputPath = Path.Combine(outputDir, $"{fileName}_print.pdf");

        double targetWidthPt = targetWidthMm * PdfPrintConstants.MmToPt;
        double targetHeightPt = targetHeightMm * PdfPrintConstants.MmToPt;

        double marginTBPt = PdfPrintConstants.MarginTopBottomMm * PdfPrintConstants.MmToPt;
        double marginLRPt = PdfPrintConstants.MarginLeftRightMm * PdfPrintConstants.MmToPt;

        Console.WriteLine($"Reading PDF: {inputPath}");

        using var inputDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
        using var outputDoc = new PdfDocument();

        Console.WriteLine($"Pages in PDF: {inputDoc.PageCount}");
        Console.WriteLine($"Target size: {targetWidthPt:F1} x {targetHeightPt:F1} pt " +
                          $"({targetWidthMm:F1} x {targetHeightMm:F1} mm)");

        for (int i = 0; i < inputDoc.PageCount; i++)
        {
            var inputPage = inputDoc.Pages[i];
            Console.WriteLine($"\nProcessing page {i + 1}: " +
                              $"{inputPage.Width.Point:F1} x {inputPage.Height.Point:F1} pt");

            var (cropX, cropY, cropW, cropH) = GetContentBoundingBox(inputPath, i);

            double scaleX = (targetWidthPt - marginLRPt * 2) / cropW;
            double scaleY = (targetHeightPt - marginTBPt * 2) / cropH;
            double scale = Math.Min(Math.Min(scaleX, scaleY), 1.0);

            double scaledW = cropW * scale;
            double scaledH = cropH * scale;
            double offsetX = (targetWidthPt - scaledW) / 2;
            double offsetY = (targetHeightPt - scaledH) / 2;

            Console.WriteLine($"  Scale: {scale:F3}, Offset: ({offsetX:F1}, {offsetY:F1})");

            var newPage = outputDoc.AddPage();
            newPage.Width = XUnit.FromPoint(targetWidthPt);
            newPage.Height = XUnit.FromPoint(targetHeightPt);

            using var gfx = XGraphics.FromPdfPage(newPage);
            var form = XPdfForm.FromFile(inputPath);
            form.PageNumber = i + 1;

            // DrawImage рисует всю страницу — сдвигаем на cropX/cropY
            double destX = offsetX - cropX * scale;
            double destY = offsetY - cropY * scale;
            double destW = inputPage.Width.Point * scale;
            double destH = inputPage.Height.Point * scale;

            gfx.Save();
            gfx.IntersectClip(new XRect(0, 0, targetWidthPt, targetHeightPt));
            gfx.DrawImage(form, destX, destY, destW, destH);
            gfx.Restore();
        }

        outputDoc.Save(outputPath);
        Console.WriteLine($"\nSaved: {outputPath}");
        return outputPath;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bounding box контента через PdfPig (координаты top-down, в points)
    // ─────────────────────────────────────────────────────────────────────────
    public static (double x, double y, double width, double height) GetContentBoundingBox(
        string pdfPath, int pageIndex = 0)
    {
        const double edgeTolerance = 2.0; // pt — элементы у края считаются рамкой документа

        using var doc = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
        var page = doc.GetPage(pageIndex + 1); // PdfPig нумерует с 1
        double pw = page.Width;
        double ph = page.Height;

        double xMin = double.MaxValue, yMin = double.MaxValue;
        double xMax = double.MinValue, yMax = double.MinValue;

        // PdfPig Y снизу вверх → конвертируем в top-down
        void Expand(double left, double bottom, double right, double top)
        {
            // Пропускаем элементы охватывающие почти всю страницу (рамки документа)
            if (left <= edgeTolerance && bottom <= edgeTolerance &&
                right >= pw - edgeTolerance && top >= ph - edgeTolerance)
                return;

            double tdTop = ph - top;
            double tdBottom = ph - bottom;

            xMin = Math.Min(xMin, left);
            xMax = Math.Max(xMax, right);
            yMin = Math.Min(yMin, tdTop);
            yMax = Math.Max(yMax, tdBottom);
        }

        foreach (var word in page.GetWords())
        {
            var bb = word.BoundingBox;
            Expand(bb.Left, bb.Bottom, bb.Right, bb.Top);
        }

        foreach (var img in page.GetImages())
        {
            var bb = img.Bounds;
            Expand(bb.Left, bb.Bottom, bb.Right, bb.Top);
        }

        if (xMin == double.MaxValue)
        {
            Console.WriteLine("  WARNING: No content found, using full page");
            return (0, 0, pw, ph);
        }

        Console.WriteLine($"  Content bbox: L={xMin:F1} T={yMin:F1} R={xMax:F1} B={yMax:F1}");
        Console.WriteLine($"  Margins: L={xMin:F1} R={pw - xMax:F1} T={yMin:F1} B={ph - yMax:F1}");

        return (xMin, yMin, xMax - xMin, yMax - yMin);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Печать через PDFtoPrinter.exe
    // ─────────────────────────────────────────────────────────────────────────
    public static async Task PrintPdfAsync(string pdfPath, string printerName, int copies = 1)
    {
        var exeDir = AppContext.BaseDirectory;
        var pdfToPrinterPath = Path.Combine(exeDir, PdfPrintConstants.PdfToPrinterExe);

        if (!File.Exists(pdfToPrinterPath))
        {
            Console.WriteLine($"\n❌ ERROR: {PdfPrintConstants.PdfToPrinterExe} not found!");
            Console.WriteLine($"Expected location: {pdfToPrinterPath}");
            Console.WriteLine($"Download from: {PdfPrintConstants.PdfToPrinterUrl}");
            return;
        }

        Console.WriteLine($"Using: {pdfToPrinterPath}");
        Console.WriteLine($"Printing to: {printerName}");
        Console.WriteLine($"Copies: {copies}");

        for (int i = 0; i < copies; i++)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = pdfToPrinterPath,
                Arguments = $"\"{pdfPath}\" \"{printerName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Console.Write($"Printing copy {i + 1}/{copies}... ");

            using var process = Process.Start(startInfo);
            if (process is null) continue;

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Console.WriteLine("❌ FAILED");
                if (!string.IsNullOrEmpty(error))
                    Console.WriteLine($"   Error: {error}");
            }
            else
            {
                Console.WriteLine("✓ Sent to queue");
                if (!string.IsNullOrEmpty(output))
                    Console.WriteLine($"   {output}");
            }
        }

        Console.WriteLine("\n✓ All print jobs sent to printer queue!");
    }
}
