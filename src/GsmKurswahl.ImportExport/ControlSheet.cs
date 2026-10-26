using System.Text;
using GsmKurswahl.Domain;
using SkiaSharp;

namespace GsmKurswahl.ImportExport;

/// <summary>Direct offline PDF export. All rule decisions are supplied by the application.</summary>
public static class ControlSheet
{
    public static byte[] Render(CourseChoice choice, OfferCatalog catalog, ProfileContext profile,
        ValidationResult validation, IEnumerable<SemesterSummary> summaries, IEnumerable<ExamOption> exams)
    {
        using var output = new MemoryStream();
        using var document = SKDocument.CreatePdf(output, new SKDocumentPdfMetadata
        { Title = "GSM-Kurswahl - Kontrollbogen", Author = "GSM-Kurswahl", Creator = "GSM-Kurswahl" });
        using var regular = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal);
        using var bold = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        using var text = new SKPaint { Typeface = regular, TextSize = 10, IsAntialias = true, Color = new SKColor(27, 42, 60) };
        using var heading = new SKPaint { Typeface = bold, TextSize = 14, IsAntialias = true, Color = new SKColor(24, 95, 165) };
        using var muted = new SKPaint { Typeface = regular, TextSize = 8, IsAntialias = true, Color = new SKColor(85, 95, 110) };
        using var line = new SKPaint { Color = new SKColor(210, 217, 227), StrokeWidth = .6f, IsStroke = true };
        using var fill = new SKPaint { Color = new SKColor(237, 242, 248) };
        const float width = 595.28f, height = 841.89f, margin = 40, usable = width - 2 * margin, bottom = height - 48;
        SKCanvas canvas = null!;
        var y = 0f;
        var page = 0;
        void NewPage()
        {
            if (page > 0) document.EndPage();
            canvas = document.BeginPage(width, height);
            page++;
            canvas.Clear(SKColors.White);
            canvas.DrawText($"GSM-Kurswahl  /  Abitur {choice.GraduationYear}", margin, 26, muted);
            canvas.DrawLine(margin, 33, width - margin, 33, line);
            canvas.DrawText("Vorläufiger Entwurf - keine verbindliche Freigabe", margin, height - 24, muted);
            canvas.DrawText($"Seite {page}", width - margin - 40, height - 24, muted);
            y = 57;
        }
        void Ensure(float needed) { if (y + needed > bottom) NewPage(); }
        void Paragraph(string value, SKPaint? paint = null, float gap = 6)
        {
            paint ??= text;
            foreach (var row in Wrap(value, paint, usable))
            {
                Ensure(paint.TextSize + 5);
                canvas.DrawText(row, margin, y, paint);
                y += paint.TextSize + 5;
            }
            y += gap;
        }
        void Heading(string value)
        {
            Ensure(52);
            y += 10;
            Paragraph(value, heading, 8);
        }
        void TableHeader()
        {
            Ensure(28);
            canvas.DrawRect(margin, y - 12, usable, 23, fill);
            canvas.DrawText("Fach / Variante", margin + 7, y + 3, text);
            canvas.DrawText("Kursart", margin + 238, y + 3, text);
            for (var h = 1; h <= 4; h++) canvas.DrawText($"HJ {h}", margin + 285 + (h - 1) * 56, y + 3, text);
            y += 29;
        }
        NewPage();
        heading.TextSize = 22;
        Paragraph("Kurswahl - Kontrollbogen", heading, 9);
        heading.TextSize = 14;
        Paragraph("Vorläufig / nicht eingereicht", heading);
        Paragraph($"Schüler-ID: {choice.StudentId}   |   Wahlrevision: {choice.Revision}");
        Paragraph($"Angebot: {choice.OfferVersion}   |   Regeln: {choice.RuleVersion}", muted);
        Paragraph($"Wahlstand: {choice.UpdatedAt:dd.MM.yyyy HH:mm zzz}", muted);
        Paragraph($"Profil: {profile.Status}   |   Revision: {profile.Revision?.ToString() ?? "ungeprüft"}   |   Stand: {profile.IssuedAt?.ToString("dd.MM.yyyy HH:mm zzz") ?? "unbekannt"}", muted);
        Paragraph($"Prüfergebnis: {(validation.Status == ReviewStatus.Invalid ? "invalid" : "needs_review")}");
        Heading("Belegung nach Halbjahren");
        TableHeader();
        foreach (var selection in choice.Selections)
        {
            var offer = catalog.Offers.FirstOrDefault(o => o.Id == selection.OfferId);
            var name = catalog.Subjects.FirstOrDefault(s => s.Id == selection.SubjectId)?.Name ?? selection.SubjectId;
            if (selection.Variant == CourseVariant.Bilingual) name += " bilingual";
            var rows = Wrap(name, text, 222).ToArray();
            var rowHeight = Math.Max(25, rows.Length * 15 + 8);
            if (y + Math.Min(rowHeight, 200) > bottom) { NewPage(); TableHeader(); }
            var type = selection.Type switch { CourseType.Lf => "LF", CourseType.Bf => "BF", CourseType.Elective => "Wahlfach", _ => "Seminar" };
            canvas.DrawText(type, margin + 238, y, text);
            for (var h = 1; h <= 4; h++)
                canvas.DrawText(selection.Semesters.Contains(h) ? (offer?.Schedule.FirstOrDefault(s => s.Semester == h)?.WeeklyHours.ToString() ?? "?") + " Std." : "-",
                    margin + 285 + (h - 1) * 56, y, text);
            foreach (var row in rows)
            {
                if (y + 18 > bottom) { NewPage(); TableHeader(); }
                canvas.DrawText(row, margin + 7, y, text);
                y += 15;
            }
            y += 10;
            canvas.DrawLine(margin, y - 7, width - margin, y - 7, line);
        }
        Heading("Kurse und Wochenstunden");
        foreach (var summary in summaries)
            Paragraph($"HJ {summary.Semester}: {summary.CourseCount} Kurse  /  {summary.WeeklyHours} Wochenstunden", gap: 1);
        Paragraph("Belegung und Anrechnung werden getrennt geprüft: mindestens 42 belegte Kurse; höchstens 40 Pflichtanrechnungskurse.", muted);
        Heading("Mögliche mündliche Prüfungsfächer");
        var options = exams.ToArray();
        if (options.Length == 0) Paragraph("Keine passende Kombination aus der aktuellen Belegung ermittelt. Offene Punkte prüfen.");
        foreach (var option in options)
            Paragraph($"{option.First} + {option.Second}: vorläufige Mindestanrechnung {option.MinimumCredits} / needs_review", gap: 2);
        if (options.Length > 0) Paragraph("Alle Kombinationen sind vorläufig. Persönliche Voraussetzungen, Rechtsstand und Anrechnung sind zu bestätigen. BLL/SIA ersetzt kein Fach automatisch.", muted);
        Heading("Prüfhinweise und offene Entscheidungen");
        foreach (var message in validation.Messages)
        {
            var blockHeight = Wrap(message.Rule.Id, heading, usable).Count() * (heading.TextSize + 5) + 1
                + Wrap(message.Explanation, text, usable).Count() * (text.TextSize + 5) + 6
                + Wrap(message.Remedy, muted, usable).Count() * (muted.TextSize + 5) + 10;
            Ensure(Math.Min(blockHeight, bottom - 57));
            Paragraph(message.Rule.Id, heading, 1);
            Paragraph(message.Explanation);
            Paragraph(message.Remedy, muted, 10);
        }
        Paragraph("Die Oberstufenberatung prüft gegen den aktuellen geschützten Stammdatenbestand, das Angebot und den Rechtsstand. Dieser Kontrollbogen dokumentiert ausschließlich den vorläufigen Entwurf.", muted);
        document.EndPage();
        document.Close();
        return output.ToArray();
    }

    // Measure actual glyph widths, split even a long ID, and preserve explicit paragraphs.
    private static IEnumerable<string> Wrap(string value, SKPaint paint, float maxWidth)
    {
        foreach (var paragraph in value.Replace("\r", "").Split('\n'))
        {
            var current = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (paint.MeasureText(candidate) <= maxWidth) { current = candidate; continue; }
                if (current.Length > 0) { yield return current; current = ""; }
                foreach (var rune in word.EnumerateRunes())
                {
                    if (current.Length > 0 && paint.MeasureText(current + rune) > maxWidth)
                    { yield return current; current = ""; }
                    current += rune;
                }
            }
            if (current.Length > 0) yield return current;
        }
    }
}

