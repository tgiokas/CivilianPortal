using System.Globalization;
using Microsoft.Extensions.Logging;

using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using HtmlAgilityPack;

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

        // Fonts
        var titleFont = new XFont(FontFamily, 11, XFontStyle.Bold | XFontStyle.Underline);
        var labelFont = new XFont(FontFamily, 10, XFontStyle.Bold);
        var bodyFont = new XFont(FontFamily, 11, XFontStyle.Regular);
        var boldBodyFont = new XFont(FontFamily, 11, XFontStyle.Bold);
        var smallFont = new XFont(FontFamily, 9, XFontStyle.Regular);

        var contentWidth = page.Width - PageMarginLeft - PageMarginRight;
        var y = PageMarginTop;

        // Title
        gfx.DrawString("ΒΕΒΑΙΩΣΗ ΚΑΤΑΧΩΡΗΣΗΣ ΑΙΤΗΣΗΣ", titleFont, XBrushes.Black,
            new XRect(PageMarginLeft, y, contentWidth, 24),
            XStringFormats.TopCenter);
        y += 30;

        // ── Tracking / submission metadata
        var greekCulture = CultureInfo.GetCultureInfo("el-GR");
        var submittedLocal = data.SubmittedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", greekCulture);
        var valueColumnOffset = PageMarginLeft + 180;

        // ── Θέμα
        gfx.DrawString("Θέμα:", labelFont, XBrushes.Black, PageMarginLeft, y + 10);
        y = DrawLabelValue(gfx, bodyFont, data.Subject ?? string.Empty,
            valueColumnOffset, y, contentWidth - 150);

        // ── Αριθμός Συνημμένων
        gfx.DrawString("Αριθμός Συνημμένων:", labelFont, XBrushes.Black, PageMarginLeft, y + 10);
        y = DrawLabelValue(gfx, bodyFont, data.AttachmentNumber ?? string.Empty,
            valueColumnOffset, y, contentWidth - 150);

        // ── Ονοματεπώνυμο Αποστολέα
        gfx.DrawString("Ονοματεπώνυμο Αποστολέα:", labelFont, XBrushes.Black, PageMarginLeft, y + 10);
        y = DrawLabelValue(gfx, bodyFont, data.CitizenFullName ?? string.Empty,
            valueColumnOffset, y, contentWidth - 150);

        // ── Email Αποστολέα
        gfx.DrawString("Email Αποστολέα:", labelFont, XBrushes.Black, PageMarginLeft, y + 10);
        y = DrawLabelValue(gfx, bodyFont, data.CitizenEmail ?? string.Empty,
            valueColumnOffset, y, contentWidth - 150);

        y += 30;
        // ── Body
        var bodyHeight = page.Height - PageMarginBottom - 20;

        gfx.DrawString("Κυρίως Μέρος:", labelFont, XBrushes.Black, PageMarginLeft, y + 10);
        y += 16;
        y = DrawHtmlBody(gfx, bodyFont, boldBodyFont, data.Body,
            PageMarginLeft, y, contentWidth, bodyHeight);

        // ── Footer
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
   
    /// Draws plain text starting at (<paramref name="x"/>, <paramref name="y"/>) with word-wrap
    /// and returns the updated Y cursor. Stops early if the next line would exceed <paramref name="maxY"/>.
    private static double DrawLabelValue(XGraphics gfx, XFont font, string text,
        double x, double y, double maxWidth, double maxY = double.MaxValue)
    {
        double lineHeight = 16;

        foreach (var paragraph in text.Split('\n'))
        {
            var currentLine = string.Empty;

            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = currentLine.Length == 0 ? word : currentLine + " " + word;

                if (gfx.MeasureString(candidate, font).Width > maxWidth && currentLine.Length > 0)
                {
                    if (y + 10 > maxY) return y;
                    gfx.DrawString(currentLine, font, XBrushes.Black, x, y + 10);
                    currentLine = word;
                    y += lineHeight;
                }
                else
                {
                    currentLine = candidate;
                }
            }

            if (currentLine.Length > 0 && y + 10 <= maxY)
                gfx.DrawString(currentLine, font, XBrushes.Black, x, y + 10);

            y += lineHeight;
        }

        return y;
    }
   
    /// Parses <paramref name="html"/> into styled word tokens, then lays them out
    /// left-to-right with word-wrap and variable line heights for headings.
    /// Returns the updated Y cursor.    
    private static double DrawHtmlBody(XGraphics gfx, XFont regularFont, XFont boldFont,
        string html, double x, double y, double maxWidth, double maxY = double.MaxValue)
    {
        var tokens = ParseHtmlToTokens(html);
        var fontCache = new Dictionary<(int size, bool bold), XFont>
        {
            { (11, false), regularFont },
            { (11, true),  boldFont    },
        };

        var currentLine = new List<(string text, XFont font, bool underline, double xOffset, int fontSize)>();
        double lineX = 0;

        foreach (var token in tokens)
        {
            if (y > maxY) break;

            if (token is null) { FlushHtmlLine(gfx, x, currentLine, ref lineX, ref y); continue; }

            var font = GetOrCreateFont(fontCache, token.FontSize, token.Bold);
            var wordWidth = gfx.MeasureString(token.Text, font).Width;

            if (lineX > 0 && lineX + wordWidth > maxWidth)
                FlushHtmlLine(gfx, x, currentLine, ref lineX, ref y);

            if (lineX == 0 && string.IsNullOrWhiteSpace(token.Text))
                continue;

            currentLine.Add((token.Text, font, token.Underline, lineX, token.FontSize));
            lineX += wordWidth;
        }

        if (y <= maxY) FlushHtmlLine(gfx, x, currentLine, ref lineX, ref y);

        return y;
    }
   
    /// Returns a cached <see cref="XFont"/> for the given size and weight, creating it on first use.   
    private static XFont GetOrCreateFont(Dictionary<(int size, bool bold), XFont> cache, int size, bool bold)
    {
        var key = (size, bold);
        if (!cache.TryGetValue(key, out var font))
        {
            font = new XFont(FontFamily, size, bold ? XFontStyle.Bold : XFontStyle.Regular);
            cache[key] = font;
        }
        return font;
    }
   
    /// Draws all tokens accumulated for the current line, then advances <paramref name="y"/>
    /// by a line height proportional to the largest font on that line and clears the buffer.   
    private static void FlushHtmlLine(
        XGraphics gfx, double x,
        List<(string text, XFont font, bool underline, double xOffset, int fontSize)> currentLine,
        ref double lineX, ref double y)
    {
        if (currentLine.Count == 0) { y += 16; return; }

        var maxSize = currentLine.Max(t => t.fontSize);
        var lh = Math.Ceiling(maxSize * 1.5);
        var baseline = y + maxSize;

        foreach (var (text, font, underline, xOffset, _) in currentLine)
        {
            gfx.DrawString(text, font, XBrushes.Black, x + xOffset, baseline);
            if (underline)
            {
                var w = gfx.MeasureString(text, font).Width;
                gfx.DrawLine(XPens.Black, x + xOffset, baseline + 2, x + xOffset + w, baseline + 2);
            }
        }

        currentLine.Clear();
        lineX = 0;
        y += lh;
    }

    /// Walks the HTML document and converts it to a flat list of <see cref="HtmlToken"/> items.
    /// A <see langword="null"/> entry represents a line break.
    /// Leading and trailing line breaks are stripped.    
    private static List<HtmlToken?> ParseHtmlToTokens(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var tokens = new List<HtmlToken?>();
        WalkHtmlNode(doc.DocumentNode, false, false, false, 11, tokens);

        while (tokens.Count > 0 && tokens[0] is null) tokens.RemoveAt(0);
        while (tokens.Count > 0 && tokens[^1] is null) tokens.RemoveAt(tokens.Count - 1);

        return tokens;
    }
   
    /// Recursively visits an <see cref="HtmlNode"/>, inheriting formatting flags down the tree
    /// and appending word tokens (or <see langword="null"/> line-break sentinels) to <paramref name="tokens"/>.    
    private static void WalkHtmlNode(HtmlNode node, bool bold, bool underline, bool italic, int fontSize, List<HtmlToken?> tokens)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText)
                .Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");

            foreach (var word in SplitIntoWordTokens(text))
                tokens.Add(new HtmlToken(word, bold, underline, italic, fontSize));
            return;
        }

        if (node.NodeType != HtmlNodeType.Element)
        {
            foreach (var child in node.ChildNodes)
                WalkHtmlNode(child, bold, underline, italic, fontSize, tokens);
            return;
        }

        var tag = node.Name.ToLowerInvariant();
        bool b = bold || tag is "b" or "strong" or "h1" or "h2" or "h3";
        bool u = underline || tag is "u";
        bool it = italic || tag is "i" or "em";
        int size = tag switch { "h1" => 18, "h2" => 15, "h3" => 13, _ => fontSize };

        if (tag == "br") { tokens.Add(null); return; }

        bool isBlock = tag is "p" or "div" or "h1" or "h2" or "h3";

        // ensure a line break without adding a second, one if the last token was already a line break
        if (isBlock && tokens.Count > 0 && tokens.Last() is not null)
            tokens.Add(null);

        foreach (var child in node.ChildNodes)
            WalkHtmlNode(child, b, u, it, size, tokens);

        if (isBlock && tokens.Count > 0 && tokens.Last() is not null)
            tokens.Add(null);
    }
      
    /// Splits a plain-text string into word tokens, preserving a trailing space on each word
    /// so that tokens can be concatenated without losing inter-word spacing.    
    private static IEnumerable<string> SplitIntoWordTokens(string text)
    {
        var parts = text.Split(' ');
        for (int i = 0; i < parts.Length; i++)
        {
            var word = i < parts.Length - 1 ? parts[i] + " " : parts[i];
            if (word.Length > 0)
            {
                yield return word;
            }
        }
    }
}