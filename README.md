![STACKCOLLIDER](banner.png)

# PDF Shipping Label Printer for Thermal Printers

> **Nexus Claude:** *Every time a developer is confident a task will take 20 minutes — somewhere in the world, a new carrier appears with their own PDF format.*

Automatic PDF shipping label printing for thermal printers. Works with any carrier API — no operator configuration required.

---

## The Problem

A warehouse works with multiple shipping carriers. Each returns a label PDF through its own API. One printer: **TSC MB240**, paper **104×152 mm**.

The task sounds simple:
> Get PDF → Print on printer.

The catch: PDFs come from external sources, we don't control their format. One carrier sends A5, another sends near 4×6", a third sends with 270° rotation. Everything must print on the same printer, automatically, without operator involvement.

> *The warehouse worker shouldn't think about formats. Scan the order — label comes out. Period.*

---

## The Journey: A Chronicle of Failures

### Attempt 1: PrintingOptions

First instinct — .NET has `PrintingOptions`, surely we can set paper size and scale:

```csharp
var options = new PrintingOptions(printerName, filePath)
{
    PaperSize = PaperSize.A5,   // logical?
    ScaleType = ScaleType.Fit
};
```

Compiler response: `PaperSize does not contain definition for 'A5'`, and `ScaleType` doesn't exist at all.

**Result:** ❌ 5 minutes — wasted.

> **Nexus Claude:** *Developer searching for an API that doesn't exist. Like looking for a light switch in a dark room — confidence is there, switch is not.*

---

### Attempt 2: iText7

iText7 — powerful library, does everything. Installing via NuGet…

```
itext7 version 8.x — license: AGPL v3
```

AGPLv3 in a commercial product means: your entire codebase becomes open source. Or buy a commercial license.

**Result:** ❌ 20 minutes — legal landmine.

> **Nexus Claude:** *A license like a time bomb — goes off exactly when the project is already in production. AGPLv3 is written in fine print that everyone sees too late.*

---

### Attempt 3: PdfiumViewer

Found an alternative — `PdfiumViewer`, good reviews. Installing...

```
Package 'PdfiumViewer' was restored using '.NETFramework,Version=v4.x'
instead of the project target framework 'net8.0'.
```

Incompatible with .NET 8.

**Result:** ❌ 30 minutes — miss.

> **Nexus Claude:** *Three dead ends in one hour. The pattern is clear — nobody asked the printer what it thinks about its own paper size.*

---

## The Working Stack

Three MIT-compatible libraries:

| Library | Purpose |
|---|---|
| `PdfSharp 6.2.4` | PDF cropping and scaling |
| `UglyToad.PdfPig` | Page content analysis (bounding box) |
| `PDFtoPrinter` | Sending final PDF to Windows printer |

---

## Problem: Fixed Margins Don't Work

First idea — hardcode margins based on one carrier's analysis:

```
Carrier1: left 73.5pt, right 74.5pt, top 33pt, bottom 85pt
```

Works for one carrier. But another has the label frame right at the page edge (±0.1pt). Fixed margins clip the content.

> **Nexus Claude:** *Every carrier is convinced their format is the only correct one. They're both right. That's exactly why we have a problem.*

**Conclusion:** need dynamic crop — analyze where content actually lives in each PDF.

---

## Dynamic Crop via PdfPig

`UglyToad.PdfPig` reads coordinates of all elements: text, images, vector graphics.

```csharp
static (double left, double top, double right, double bottom)
    GetContentBoundingBox(string pdfPath, int pageIndex)
{
    using var doc = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
    var page = doc.GetPage(pageIndex + 1);

    double minX = double.MaxValue, minY = double.MaxValue;
    double maxX = double.MinValue, maxY = double.MinValue;

    foreach (var word in page.GetWords())
    {
        minX = Math.Min(minX, word.BoundingBox.Left);
        minY = Math.Min(minY, word.BoundingBox.Bottom);
        maxX = Math.Max(maxX, word.BoundingBox.Right);
        maxY = Math.Max(maxY, word.BoundingBox.Top);
    }

    foreach (var img in page.GetImages())
    {
        var b = img.Bounds;
        minX = Math.Min(minX, b.Left);
        minY = Math.Min(minY, b.Bottom);
        maxX = Math.Max(maxX, b.Right);
        maxY = Math.Max(maxY, b.Top);
    }

    // Filter decorative page border frames (±2pt)
    const double margin = 2.0;
    double pw = page.Width, ph = page.Height;
    foreach (var path in page.ExperimentalAccess.Paths)
        foreach (var cmd in path)
        {
            if (cmd.X < margin || cmd.Y < margin ||
                cmd.X > pw - margin || cmd.Y > ph - margin) continue;
            minX = Math.Min(minX, cmd.X);
            minY = Math.Min(minY, cmd.Y);
            maxX = Math.Max(maxX, cmd.X);
            maxY = Math.Max(maxY, cmd.Y);
        }

    return (minX, ph - maxY, maxX, ph - minY);
}
```

Critical: **±2pt filter**. Without it, the decorative page border collapsed the entire crop to zero.

---

## Scale Fit-to-Page

```csharp
static string CropAndScalePdf(string inputPath, double targetWidthMm, double targetHeightMm)
{
    const double mmToPt = 2.834645669;
    const double marginTopBottomMm = 5.0;
    const double marginLeftRightMm = 6.0;

    using var inputDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
    using var outputDoc = new PdfSharp.Pdf.PdfDocument();

    for (int i = 0; i < inputDoc.PageCount; i++)
    {
        var inputPage = inputDoc.Pages[i];

        // Rotated pages — copy as-is, printer handles it
        if (inputPage.Rotate != 0)
        {
            outputDoc.AddPage(inputDoc.Pages[i]);
            continue;
        }

        var newPage = outputDoc.AddPage();
        newPage.Width  = targetWidthMm * mmToPt;
        newPage.Height = targetHeightMm * mmToPt;

        var (cropL, cropT, cropR, cropB) = GetContentBoundingBox(inputPath, i);
        double cropW = cropR - cropL;
        double cropH = cropB - cropT;

        double targetW = newPage.Width  - 2 * marginLeftRightMm * mmToPt;
        double targetH = newPage.Height - 2 * marginTopBottomMm * mmToPt;

        // Never upscale small labels
        double scale = Math.Min(Math.Min(targetW / cropW, targetH / cropH), 1.0);

        double scaledW = cropW * scale;
        double scaledH = cropH * scale;
        double offsetX = (newPage.Width  - scaledW) / 2;
        double offsetY = (newPage.Height - scaledH) / 2;

        using var gfx = XGraphics.FromPdfPage(newPage);
        var form = XPdfForm.FromFile(inputPath);
        form.PageNumber = i + 1;

        gfx.Save();
        gfx.IntersectClip(new XRect(offsetX, offsetY, scaledW, scaledH));
        gfx.TranslateTransform(offsetX, offsetY);
        gfx.ScaleTransform(scale);
        gfx.DrawImage(form, -cropL, -cropT, inputPage.Width, inputPage.Height);
        gfx.Restore();
    }

    var outputPath = Path.Combine(
        Path.GetDirectoryName(inputPath)!,
        Path.GetFileNameWithoutExtension(inputPath) + "_print.pdf");
    outputDoc.Save(outputPath);
    return outputPath;
}
```

---

## Rotation 270°: Two Days of Fighting, One Line of Solution

One carrier returned PDFs with `Rotation: 270°`. Everything else worked perfectly — this file printed sideways.

Three versions were attempted: coordinate transformation, clip by crop area, normalization via temporary PDF with `Rotate=0`. Each fixed one thing and broke another.

**Final solution:**

```csharp
// Rotated page — copy as-is, the printer handles it just fine
if (inputPage.Rotate != 0)
{
    outputDoc.AddPage(inputDoc.Pages[i]);
    continue;
}
```

> **Nexus Claude:** *The best solution to the rotation 270° problem — don't solve it. Sometimes the most elegant code is the code that doesn't exist.*

---

## Result

```
Carrier API → PDF (any format) → CropAndScalePdf() → Label Printer
```

One endpoint. Any carrier. Zero operator configuration.

---

## Installation

```xml
<PackageReference Include="PDFsharp" Version="6.2.4" />
<PackageReference Include="UglyToad.PdfPig" Version="0.1.9" />
<PackageReference Include="PDFtoPrinter" Version="1.3.0" />
```

---

## Usage

```csharp
// 1. Get PDF from carrier API
var pdfPath = await carrier.GetLabelAsync(orderId);

// 2. Crop and scale to printer paper size (104×152mm)
var printPath = CropAndScalePdf(pdfPath, targetWidthMm: 104, targetHeightMm: 152);

// 3. Send to printer
await PDFtoPrinter.PrintAsync(printerName, printPath);
```

---

> **Nexus Claude:** *Four days. Six dead ends. One stub that solved everything. SVUL works exactly like this — reality is always more complex than it seems, and simpler than you fear.*

---

*STACKCOLLIDER & Nexus Claude*
