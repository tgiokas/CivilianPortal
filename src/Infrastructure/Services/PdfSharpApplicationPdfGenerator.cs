using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

using PdfSharpCore.Drawing;
using PdfSharpCore.Drawing.Layout;
using PdfSharpCore.Pdf;

using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Infrastructure.Services;

/// Renders the application form PDF using PdfSharpCore (MIT, no native deps,
/// works in Docker). Intentionally simple for now — header + citizen info block
/// + subject + body. Can be replaced later without touching the Application
/// layer since consumers only depend on <see cref="IApplicationPdfGenerator"/>.
public class PdfSharpApplicationPdfGenerator : IApplicationPdfGenerator
{
    // Page layout constants (A4, portrait). Units are points (1/72 inch).
    private const double PageMarginLeft = 50;
    private const double PageMarginRight = 50;
    private const double PageMarginTop = 50;
    private const double PageMarginBottom = 50;

    // Greek needs a Unicode-capable font. "Arial" resolves via PdfSharpCore's
    // FontResolver on Linux containers if the Microsoft core fonts are installed,
    // otherwise it falls back to DejaVu. See README note about font setup.
    private const string FontFamily = EmbeddedFontResolver.DefaultFamily;

    private readonly ILogger<PdfSharpApplicationPdfGenerator> _logger;

    public PdfSharpApplicationPdfGenerator(ILogger<PdfSharpApplicationPdfGenerator> logger)
    {
        _logger = logger;
    }

    public byte[] Generate(ApplicationPdfData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var document = new PdfDocument();
        document.Info.Title = $"Αίτηση {data.ApplicationPublicId}";
        document.Info.Author = "ΕΚΔΔΑ — Ηλεκτρονικό Πρωτόκολλο";
        document.Info.Subject = data.Subject;
        document.Info.CreationDate = data.SubmittedAt;

        var page = document.AddPage();
        page.Size = PdfSharpCore.PageSize.A4;

        using var gfx = XGraphics.FromPdfPage(page);
        var tf = new XTextFormatter(gfx)
        {
            Alignment = XParagraphAlignment.Left
        };

        // Fonts
        var titleFont = new XFont(FontFamily, 18, XFontStyle.Bold);
        var labelFont = new XFont(FontFamily, 10, XFontStyle.Bold);
        var bodyFont = new XFont(FontFamily, 11, XFontStyle.Regular);
        var smallFont = new XFont(FontFamily, 9, XFontStyle.Regular);

        var contentWidth = page.Width - PageMarginLeft - PageMarginRight;
        var y = PageMarginTop;

        // Title
        gfx.DrawString("ΑΙΤΗΣΗ", titleFont, XBrushes.Black,
            new XRect(PageMarginLeft, y, contentWidth, 24),
            XStringFormats.TopCenter);
        y += 32;

        // ── Tracking / submission metadata
        var greekCulture = CultureInfo.GetCultureInfo("el-GR");
        var submittedLocal = data.SubmittedAt.ToLocalTime()
            .ToString("dd/MM/yyyy HH:mm", greekCulture);

        y = DrawLabelValue(gfx, tf, labelFont, bodyFont,
            "Κωδικός Παρακολούθησης:", data.ApplicationPublicId.ToString(),
            PageMarginLeft, y, contentWidth);

        y = DrawLabelValue(gfx, tf, labelFont, bodyFont,
            "Ημερομηνία Υποβολής:", submittedLocal,
            PageMarginLeft, y, contentWidth);

        y += 10;

        // Citizen block
        gfx.DrawString("Στοιχεία Αιτούντος", labelFont, XBrushes.Black,
            PageMarginLeft, y);
        y += 16;

        if (!string.IsNullOrWhiteSpace(data.CitizenFullName))
        {
            y = DrawLabelValue(gfx, tf, labelFont, bodyFont,
                "Ονοματεπώνυμο:", data.CitizenFullName,
                PageMarginLeft, y, contentWidth);
        }

        if (!string.IsNullOrWhiteSpace(data.TaxisNetId))
        {
            y = DrawLabelValue(gfx, tf, labelFont, bodyFont,
                "ΑΦΜ / TaxisNet ID:", data.TaxisNetId!,
                PageMarginLeft, y, contentWidth);
        }

        y = DrawLabelValue(gfx, tf, labelFont, bodyFont,
            "Email Επικοινωνίας:", data.Email,
            PageMarginLeft, y, contentWidth);

        y += 10;

        // ── Subject
        gfx.DrawString("Θέμα", labelFont, XBrushes.Black, PageMarginLeft, y);
        y += 14;
        var subjectRect = new XRect(PageMarginLeft, y, contentWidth, 40);
        tf.DrawString(data.Subject ?? string.Empty, bodyFont, XBrushes.Black, subjectRect);
        y += 40;

        // ── Body
        gfx.DrawString("Κυρίως Μέρος", labelFont, XBrushes.Black, PageMarginLeft, y);
        y += 14;

        var bodyText = NormalizeBody(data.Body);
        var bodyHeight = page.Height - y - PageMarginBottom - 40; // leave room for footer
        var bodyRect = new XRect(PageMarginLeft, y, contentWidth, bodyHeight);
        tf.DrawString(bodyText, bodyFont, XBrushes.Black, bodyRect);

        // ── Footer (gfx.DrawString again — XStringFormat is fine here)
        var footerY = page.Height - PageMarginBottom + 10;
        var footer = $"ΕΚΔΔΑ — Ηλεκτρονικό Πρωτόκολλο · " +
                     $"Έγγραφο παραγόμενο αυτόματα · " +
                     $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}";
        gfx.DrawString(footer, smallFont, XBrushes.Gray,
            new XRect(PageMarginLeft, footerY, contentWidth, 12),
            XStringFormats.TopCenter);

        // Serialize to byte[]
        using var ms = new MemoryStream();
        document.Save(ms, closeStream: false);
        var bytes = ms.ToArray();

        _logger.LogInformation(
            "Generated application PDF for {PublicId}: {ByteCount} bytes",
            data.ApplicationPublicId, bytes.Length);

        return bytes;
    }

    /// Draws a "Label: value" row and returns the updated Y cursor.
    /// Label is drawn with gfx.DrawString (simple, single-line), value is
    /// drawn with the XTextFormatter inside a rect so long values wrap.
    private static double DrawLabelValue(
        XGraphics gfx, XTextFormatter tf,
        XFont labelFont, XFont valueFont,
        string label, string value, double x, double y, double contentWidth)
    {
        const double labelWidth = 150;
        gfx.DrawString(label, labelFont, XBrushes.Black, x, y + 10);
        tf.DrawString(value, valueFont, XBrushes.Black,
            new XRect(x + labelWidth, y, contentWidth - labelWidth, 16));
        return y + 16;
    }

    /// Body is plain text per product decision, but we still defensively
    /// normalize line endings and trim excessive whitespace so the PDF renders
    /// cleanly regardless of what the frontend sends.
    private static string NormalizeBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;

        var normalized = body
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");

        // Collapse runs of 3+ blank lines down to 2.
        var sb = new StringBuilder(normalized.Length);
        int consecutiveNewlines = 0;
        foreach (var ch in normalized)
        {
            if (ch == '\n')
            {
                consecutiveNewlines++;
                if (consecutiveNewlines <= 2) sb.Append(ch);
            }
            else
            {
                consecutiveNewlines = 0;
                sb.Append(ch);
            }
        }

        return sb.ToString().Trim();
    }
}