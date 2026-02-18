using Microsoft.VisualBasic;
using PdfPrinterTool;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("=== PRINTER DIAGNOSTICS & PDF CONVERSION TEST ===\n");

var printers = PrinterHelper.GetInstalledPrinters();

if (printers.Count == 0)
{
    Console.WriteLine("No printers found!");
    return;
}

Console.WriteLine($"Found printers: {printers.Count}\n");
Console.WriteLine(new string('=', 80));

for (int i = 0; i < printers.Count; i++)
    Console.WriteLine($"{i + 1}. {printers[i]}");

Console.WriteLine(new string('=', 80));
Console.Write("\nSelect printer number (or 0 to show all details): ");
var choice = int.Parse(Console.ReadLine() ?? "0");

if (choice == 0)
{
    Console.WriteLine("\n" + new string('=', 80));
    foreach (var name in printers)
    {
        try
        {
            PrinterHelper.PrintInfo(name);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
        Console.WriteLine(new string('=', 80));
    }
}
else if (choice > 0 && choice <= printers.Count)
{
    var selectedPrinter = printers[choice - 1];

    Console.WriteLine("\n" + new string('=', 80));
    PrinterHelper.PrintInfo(selectedPrinter);
    Console.WriteLine(new string('=', 80));

    Console.WriteLine("\nSelect action:");
    Console.WriteLine("  1. Convert & Print PDF files");
    Console.WriteLine("  2. Test print");
    Console.Write("Choice: ");
    var action = Console.ReadLine()?.Trim();

    if (action == "2")
    {
        Console.WriteLine("\n=== TEST PRINT ===");
        var (widthMm, heightMm) = PrinterHelper.GetPaperSizeMm(selectedPrinter);

        var pdfBytes = TestPdfGenerator.Generate(selectedPrinter, "localhost", widthMm, heightMm);
        var tempPath = Path.Combine(PdfPrintConstants.TestPrintersDir, $"TestPrint_{Guid.NewGuid()}.pdf");
        await File.WriteAllBytesAsync(tempPath, pdfBytes);

        await PdfProcessor.PrintPdfAsync(tempPath, selectedPrinter);
        await Task.Delay(5000);
        // TODO: раскомментировать когда убедимся что файл не нужен после печати
        // if (File.Exists(tempPath)) File.Delete(tempPath);
    }
    else
    {
        try
        {
            Console.WriteLine("\n=== PDF CONVERSION ===");
            var (widthMm, heightMm) = PrinterHelper.GetPaperSizeMm(selectedPrinter);
            Console.WriteLine($"Target size: {widthMm:F1} x {heightMm:F1} mm");

            var pdfFiles = Enumerable.Range(1, 6)
                .Select(n => Path.Combine(PdfPrintConstants.TestPrintersDir, $"{n}.pdf"))
                .Where(File.Exists)
                .ToList();

            if (pdfFiles.Count == 0)
            {
                Console.WriteLine("No files 1.pdf-6.pdf found!");
            }
            else
            {
                Console.WriteLine($"Found {pdfFiles.Count} file(s) to process\n");

                var convertedPaths = new List<string>();
                foreach (var file in pdfFiles)
                {
                    Console.WriteLine(new string('-', 60));
                    convertedPaths.Add(PdfProcessor.CropAndScalePdf(file, widthMm, heightMm));
                    Console.WriteLine($"✓ Done: {Path.GetFileName(convertedPaths[^1])}");
                }

                Console.WriteLine(new string('=', 60));
                Console.WriteLine($"Converted: {convertedPaths.Count} file(s)");

                Console.Write("\nPrint all to this printer? (y/n): ");
                if (Console.ReadLine()?.ToLower() == "y")
                {
                    Console.WriteLine("\n=== PRINTING ===");
                    foreach (var path in convertedPaths)
                    {
                        Console.WriteLine($"Printing: {Path.GetFileName(path)}");
                        await PdfProcessor.PrintPdfAsync(path, selectedPrinter);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nERROR during conversion/printing: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}

Console.WriteLine("\nPress Enter to exit...");
Console.ReadLine();

