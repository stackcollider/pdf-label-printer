namespace PdfPrinterTool;

internal static class PdfPrintConstants
{
    /// <summary>Points per millimeter (1 mm = 72/25.4 pt)</summary>
    public const double MmToPt = 2.834645669;

    /// <summary>Hundredths-of-inch to millimeters (Windows PrinterSettings units)</summary>
    public const double HundredthInchToMm = 25.4 / 100.0;

    public const double MarginTopBottomMm = 5.0;
    public const double MarginLeftRightMm = 6.0;

    public const string TestPrintersDir = @"C:\ScanToBc\TestPrinters\";
    public const string PdfToPrinterExe = "PDFtoPrinter.exe";
    public const string PdfToPrinterUrl = "https://github.com/dtopuzov/PDFtoPrinter/releases/download/v2.1/PDFtoPrinter.exe";
}
