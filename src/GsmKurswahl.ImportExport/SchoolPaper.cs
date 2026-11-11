using SkiaSharp;

namespace GsmKurswahl.ImportExport;

/// <summary>Shared offline A4 stationery distilled from GSM_Vorlage_Dokumente.docx.</summary>
internal sealed class SchoolPaper : IDisposable
{
    private readonly MemoryStream output = new();
    private readonly SKDocument document;
    private readonly SKPaint text, bold, muted, line, fill;
    private readonly SKTypeface regularFace, boldFace, narrowFace;
    private readonly SKBitmap logo;
    private SKCanvas canvas = null!;
    private readonly int year;
    private string context;
    private string reportTitle = "";
    private float y;
    private int page;
    private bool finished;
    private const float Left = 70.9f, Width = 453.5f, Bottom = 799;
    public SchoolPaper(string context, int year)
    {
        this.context = context; this.year = year;
        document = SKDocument.CreatePdf(output, new SKDocumentPdfMetadata { Title = context, Author = "Gymnasium bei St. Michael", Creator = "GSM-Kurswahl" });
        regularFace = SKTypeface.FromFamilyName("Arial");
        boldFace = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        narrowFace = SKTypeface.FromFamilyName("Arial Narrow", SKFontStyle.Bold);
        text = new() { Typeface = regularFace, TextSize = 9.5f, IsAntialias = true, Color = SKColors.Black };
        bold = new() { Typeface = boldFace, TextSize = 11, IsAntialias = true, Color = SKColors.Black };
        muted = new() { Typeface = regularFace, TextSize = 8, IsAntialias = true, Color = new SKColor(80, 80, 80) };
        line = new() { Color = new SKColor(170, 170, 170), StrokeWidth = .5f };
        fill = new() { Color = new SKColor(240, 240, 240) };
        using var source = typeof(SchoolPaper).Assembly.GetManifestResourceStream("GsmKurswahl.ImportExport.Assets.SchoolLogo.jpeg")!;
        logo = SKBitmap.Decode(source);
        NewPage();
    }
    private void NewPage()
    {
        if (page > 0) document.EndPage();
        canvas = document.BeginPage(595.28f, 841.89f); page++; canvas.Clear(SKColors.White);
        using var theme = new SKPaint { Typeface = narrowFace, TextSize = 22, Color = SKColors.Black, IsAntialias = true };
        canvas.DrawText("Kurswahl", Left, 77, theme);
        theme.TextSize = 10;
        canvas.DrawText("Oberstufenberatung · " + DateTime.Now.ToString("dd.MM.yyyy"), Left, 91, theme);
        using var imagePaint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
        canvas.DrawBitmap(logo, new SKRect(Left + Width - 148.54f, 56.7f, Left + Width, 104.32f), imagePaint);
        var footer = $"Abitur {year} · {context}";
        var footerRows = Wrap(footer, muted, Width - 65).Take(2).ToArray();
        for (var i = 0; i < footerRows.Length; i++) canvas.DrawText(footerRows[i], Left, 817 + i * 10, muted);
        canvas.DrawText("Seite " + page, Left + Width - 42, 817, muted);
        y = 134;
        if (reportTitle.Length > 0) Title(reportTitle);
    }
    public void StartStudent(string label) { context = label; reportTitle = ""; NewPage(); }
    public void Ensure(float height) { if (y + height > Bottom) NewPage(); }
    public void Title(string value)
    {
        reportTitle = value;
        using var title = new SKPaint { Typeface = boldFace, TextSize = 14, IsAntialias = true, Color = SKColors.Black };
        var rows = Wrap(value, title, Width - 20).ToArray();
        var height = Math.Max(31.2f, rows.Length * 18 + 12);
        using var border = new SKPaint { Color = SKColors.Black, StrokeWidth = .6f, Style = SKPaintStyle.Stroke };
        canvas.DrawRect(Left, y, Width, height, border);
        for (var i = 0; i < rows.Length; i++) canvas.DrawText(rows[i], Left + (Width - title.MeasureText(rows[i])) / 2, y + 21 + i * 18, title);
        y += height + 19;
    }
    public void Heading(string value) { Ensure(55); y += 6; Paragraph(value, false, bold); }
    public void Paragraph(string value, bool small = false, SKPaint? paint = null)
    {
        paint ??= small ? muted : text;
        foreach (var row in Wrap(value, paint, Width))
        {
            Ensure(paint.TextSize + 4); canvas.DrawText(row, Left, y, paint); y += paint.TextSize + 4;
        }
        y += 5;
    }
    public void BlankLines(int count)
    {
        Ensure(count * 25);
        for (var i = 0; i < count; i++) { y += 25; canvas.DrawLine(Left, y - 8, Left + Width, y - 8, line); }
    }
    public void Signature()
    {
        Ensure(60); y += 28;
        canvas.DrawLine(Left, y, Left + 130, y, line);
        canvas.DrawLine(Left + 170, y, Left + Width, y, line);
        canvas.DrawText("Ort, Datum", Left, y + 14, muted);
        canvas.DrawText("Unterschrift Schülerin / Schüler", Left + 170, y + 14, muted); y += 32;
    }
    public void Table(string[] headers, string[][] rows, float[] proportions)
    {
        if (headers.Length != proportions.Length || rows.Any(r => r.Length != headers.Length)) throw new ArgumentException("Ungültige Drucktabelle.");
        var widths = proportions.Select(w => w * Width / proportions.Sum()).ToArray();
        var headerCells = headers.Select((v, i) => Wrap(v, text, widths[i] - 10).ToArray()).ToArray();
        var headerHeight = Math.Max(24, headerCells.Max(c => c.Length) * 13 + 10);
        void Header()
        {
            canvas.DrawRect(Left, y - 11, Width, headerHeight, fill);
            float x = Left;
            for (var c = 0; c < headers.Length; c++)
            {
                for (var l = 0; l < headerCells[c].Length; l++) canvas.DrawText(headerCells[c][l], x + 5, y + l * 13, text);
                x += widths[c];
            }
            y += headerHeight;
        }
        Ensure(headerHeight + 30); Header();
        foreach (var row in rows)
        {
            var cells = row.Select((v, i) => Wrap(v, text, widths[i] - 10).ToArray()).ToArray();
            var count = Math.Max(1, cells.Max(c => c.Length));
            var offset = 0;
            while (offset < count)
            {
                var height = Math.Max(20, (count - offset) * 13 + 7);
                if (y + height > Bottom && y > 240) { NewPage(); Header(); }
                var lines = Math.Min(count - offset, Math.Max(1, (int)((Bottom - y - 7) / 13)));
                float x = Left;
                for (var c = 0; c < cells.Length; c++)
                {
                    for (var l = 0; l < lines && offset + l < cells[c].Length; l++) canvas.DrawText(cells[c][offset + l], x + 5, y + l * 13, text);
                    x += widths[c];
                }
                y += Math.Max(20, lines * 13 + 7);
                canvas.DrawLine(Left, y - 12, Left + Width, y - 12, line);
                offset += lines;
                if (offset < count) { NewPage(); Header(); }
            }
        }
        y += 7;
    }
    private static IEnumerable<string> Wrap(string value, SKPaint paint, float width)
    {
        foreach (var paragraph in value.Replace("\r", "").Split('\n'))
        {
            var current = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (paint.MeasureText(candidate) <= width) { current = candidate; continue; }
                if (current.Length > 0) { yield return current; current = ""; }
                foreach (var rune in word.EnumerateRunes())
                {
                    if (current.Length > 0 && paint.MeasureText(current + rune) > width) { yield return current; current = ""; }
                    current += rune;
                }
            }
            if (current.Length > 0) yield return current;
        }
    }
    public byte[] Finish() { document.EndPage(); document.Close(); finished = true; return output.ToArray(); }
    public void Dispose()
    {
        if (!finished) { document.EndPage(); document.Close(); }
        document.Dispose(); output.Dispose(); logo.Dispose();
        text.Dispose(); bold.Dispose(); muted.Dispose(); line.Dispose(); fill.Dispose();
        regularFace.Dispose(); boldFace.Dispose(); narrowFace.Dispose();
    }
}
