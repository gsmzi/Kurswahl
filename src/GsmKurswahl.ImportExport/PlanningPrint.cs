using GsmKurswahl.Domain;
using SkiaSharp;

namespace GsmKurswahl.ImportExport;

/// <summary>One pupil on a control sheet printed from the protected teacher stock. Violations/Hints are supplied by the rule check.</summary>
public sealed record ControlSheetItem(StudentRecord Student, CourseChoice? Choice, string[] Violations, string[] Hints);
public sealed record CourseListPrint(string Title, string[][] Rows);

/// <summary>Offline presentation only; all selection/count decisions are supplied by Application.</summary>
public static class PlanningPrint
{
    private static string TypeLabel(CourseType type) => type switch { CourseType.Lf => "LF", CourseType.Bf => "BF", CourseType.Elective => "Wahlfach", _ => "Seminar" };
    private static string SubjectLabel(OfferCatalog catalog, string id) => catalog.Subjects.FirstOrDefault(x => x.Id == id)?.Name ?? id;

    /// <summary>Control sheets, one pupil per page, in the order supplied. Signed by the pupil; the binding confirmation is the ASV form.</summary>
    public static byte[] ControlSheets(IEnumerable<ControlSheetItem> items, OfferCatalog catalog)
    {
        var rows = items.ToArray();
        if (rows.Length == 0) throw new ArgumentException("Keine Person für den Kontrollbogen ausgewählt.");
        using var pdf = new Paper("Kontrollbogen - " + rows[0].Student.Student.ExternalStudentId, catalog.GraduationYear);
        for (var i = 0; i < rows.Length; i++)
        {
            var item = rows[i];
            if (i > 0) pdf.StartStudent("Kontrollbogen - " + item.Student.Student.ExternalStudentId);
            pdf.Title("Kurswahl: Kontrollbogen");
            pdf.Paragraph(item.Student.Student.SyntheticDisplayName + "  |  Klasse " + item.Student.Student.ClassName + "  |  " + item.Student.Student.ExternalStudentId);
            if (item.Choice is null) { pdf.Paragraph("Für diese Person liegt noch keine Kurswahl vor."); continue; }
            var table = item.Choice.Selections.OrderBy(s => s.Type).ThenBy(s => SubjectLabel(catalog, s.SubjectId), StringComparer.CurrentCulture).Select(s =>
            {
                var hours = catalog.Offers.FirstOrDefault(o => o.Id == s.OfferId)?.Schedule.Select(h => h.WeeklyHours).Distinct().ToArray() ?? [];
                return new[] { SubjectLabel(catalog, s.SubjectId) + (s.Variant == CourseVariant.Bilingual ? " bilingual" : "")
                        + (item.Choice.PlannedOralSubjects.Contains(s.SubjectId) && s.Type != CourseType.Lf ? "  (mündlich geplant)" : ""),
                    TypeLabel(s.Type), hours.Length == 0 ? "" : string.Join("/", hours), Mark(s, 1), Mark(s, 2), Mark(s, 3), Mark(s, 4) };
            }).ToArray();
            pdf.Table(["Fach / Variante", "Kursart", "Std.", "HJ 1", "HJ 2", "HJ 3", "HJ 4"], table, [235, 60, 40, 45, 45, 45, 45]);
            var counts = Enumerable.Range(1, 4).Select(h => item.Choice.Selections.Count(s => s.Semesters.Contains(h))).ToArray();
            var weekly = Enumerable.Range(1, 4).Select(h => item.Choice.Selections.Where(s => s.Semesters.Contains(h))
                .Sum(s => catalog.Offers.FirstOrDefault(o => o.Id == s.OfferId)?.Schedule.FirstOrDefault(x => x.Semester == h)?.WeeklyHours ?? 0)).ToArray();
            pdf.Paragraph($"Halbjahreskurse gesamt: {item.Choice.Selections.Sum(s => s.Semesters.Length)}  |  Kurse je Halbjahr: {string.Join(" / ", counts)}  |  Wochenstunden je Halbjahr: {string.Join(" / ", weekly)}");
            pdf.Paragraph("Geplante mündliche Prüfungsfächer (unverbindlich): " + (item.Choice.PlannedOralSubjects.IsEmpty ? "noch nicht geplant"
                : string.Join(", ", item.Choice.PlannedOralSubjects.Select(o => SubjectLabel(catalog, o)))));
            if (item.Violations.Length > 0) { pdf.Heading("Noch zu korrigieren"); foreach (var v in item.Violations) pdf.Paragraph("! " + v); }
            if (item.Hints.Length > 0) { pdf.Heading("Besonderheiten / mit der Oberstufenberatung zu klären"); foreach (var h in item.Hints) pdf.Paragraph("- " + h); }
            pdf.Paragraph("Ich habe die oben aufgeführte Kurswahl geprüft. Die endgültige Bestätigung erfolgt auf dem ASV-Formular.", true);
            pdf.Signature();
        }
        return pdf.Finish();
        static string Mark(CourseSelection s, int h) => s.Semesters.Contains(h) ? "x" : "";
    }

    /// <summary>One line per pupil of the displayed group.</summary>
    public static byte[] Overview(int year, string filter, string[][] rows)
    {
        using var pdf = new Paper("Kurswahlen - Übersicht", year);
        pdf.Title("Kurswahlen - Übersicht");
        pdf.Paragraph(filter + $"  |  {rows.Length} Personen");
        pdf.Table(["Name", "Kl.", "Leistungsfächer", "Basis- und Wahlfächer", "Mündlich", "Prüfstand"], rows, [105, 32, 68, 190, 50, 70]);
        return pdf.Finish();
    }

    /// <summary>Course lists, each on its own page. Rows: number, name, class, semesters.</summary>
    public static byte[] CourseLists(int year, IEnumerable<CourseListPrint> lists)
    {
        var all = lists.ToArray();
        if (all.Length == 0) throw new ArgumentException("Keine Kursliste vorhanden.");
        using var pdf = new Paper("Kursliste - " + all[0].Title, year);
        for (var i = 0; i < all.Length; i++)
        {
            if (i > 0) pdf.StartStudent("Kursliste - " + all[i].Title);
            pdf.Title(all[i].Title);
            pdf.Paragraph($"{all[i].Rows.Length} Schülerinnen und Schüler  |  Stand der Kurswahl, keine Kurszuteilung");
            pdf.Table(["Nr.", "Name", "Klasse", "Halbjahre"], all[i].Rows, [40, 265, 80, 130]);
        }
        return pdf.Finish();
    }

    public static byte[] Demand(int year, string source, string filter, string[][] rows)
    {
        using var pdf = new Paper("Wahlzahlen", year);
        pdf.Title("Kursplanung - Wahlzahlen");
        pdf.Paragraph(source);
        pdf.Paragraph(filter);
        pdf.Paragraph("GK/Geo: Einzelhalbjahre. Sonst gleiche Zahlen zusammengefasst; KS1/KS2 bei Abweichungen.", true);
        pdf.Table(["Fach", "LF", "LF bili", "BF", "BF bili", "Wahl"], rows, [170, 45, 48, 112, 65, 75]);
        return pdf.Finish();
    }

    public static byte[] Reselection(StudentRecord student, CourseChoice remaining, OfferCatalog catalog, string[] removed)
    {
        return Reselections([new(student, remaining, removed)], catalog);
    }

    public static byte[] Reselections(IEnumerable<ReselectionPrintItem> items, OfferCatalog catalog)
    {
        var rows = items.OrderBy(x => x.Student.Student.ClassName, StringComparer.CurrentCulture)
            .ThenBy(x => x.Student.Student.SortName().Last, StringComparer.CurrentCulture)
            .ThenBy(x => x.Student.Student.SortName().First, StringComparer.CurrentCulture)
            .ThenBy(x => x.Remaining.StudentId, StringComparer.Ordinal).ToArray();
        if (rows.Length == 0) throw new ArgumentException("Keine Nachwahlbögen vorhanden.");
        using var pdf = new Paper("Nachwahl - " + rows[0].Remaining.StudentId, catalog.GraduationYear);
        for (var i = 0; i < rows.Length; i++)
        {
            if (i > 0) pdf.StartStudent("Nachwahl - " + rows[i].Remaining.StudentId);
            WriteReselection(pdf, rows[i].Student, rows[i].Remaining, catalog, rows[i].Removed, rows[i].Notes);
        }
        return pdf.Finish();
    }

    private static void WriteReselection(Paper pdf, StudentRecord student, CourseChoice remaining, OfferCatalog catalog, string[] removed, string[] notes)
    {
        pdf.Title("Deine Kurswahl: Nachwahl");
        pdf.Paragraph(student.Student.SyntheticDisplayName + "  |  Klasse " + student.Student.ClassName);
        if (removed.Length > 0) pdf.Paragraph("Folgende gewählte Kurse können leider nicht angeboten werden:");
        else pdf.Paragraph("Deine Kurswahl muss noch ergänzt oder geändert werden:");
        foreach (var note in notes) pdf.Paragraph("- " + note);
        foreach (var id in removed)
        {
            var offer = catalog.Offers.Single(o => o.Id == id);
            var type = offer.Type switch { CourseType.Lf => "LF", CourseType.Bf => "BF", CourseType.Elective => "Wahlfach", _ => "Seminarkurs" };
            pdf.Paragraph((catalog.Subjects.Single(s => s.Id == offer.SubjectId).Name) + " - " + type
                + (offer.Variant == CourseVariant.Bilingual ? " bilingual" : ""));
        }
        pdf.Paragraph((removed.Length > 0 ? "Deshalb ist" : "Dafür ist") + " eine Nachwahl bzw. Rückmeldung erforderlich. Bitte trage deine Nachwahl und gegebenenfalls weitere Umwahlen unten ein und gib diesen Bogen unterschrieben bei der Oberstufenberatung ab. Die neue Wahl wird anschließend geprüft.");
        pdf.Heading("Deine verbleibenden Kurse");
        var rows = remaining.Selections.OrderBy(s => s.Type).ThenBy(s => s.SubjectId).Select(s => new[] {
            catalog.Subjects.Single(x => x.Id == s.SubjectId).Name + (s.Variant == CourseVariant.Bilingual ? " bilingual" : ""),
            s.Type switch { CourseType.Lf => "LF", CourseType.Bf => "BF", CourseType.Elective => "Wahlfach", _ => "Seminar" },
            string.Join(", ", s.Semesters.Select(h => "HJ " + h)) }).ToArray();
        pdf.Table(["Fach / Variante", "Kursart", "Belegung"], rows, [255, 65, 195]);
        pdf.Ensure(220);
        pdf.Heading("Nachwahl und weitere Umwahlen");
        pdf.Paragraph("Neu gewähltes Fach / Kursart / Halbjahre; bei Umwahl auch das bisherige Fach nennen.", true);
        pdf.BlankLines(3);
        pdf.Paragraph("Die endgültige Belegbarkeit wird durch die Oberstufenberatung geprüft.", true);
        pdf.Signature();
    }

    private sealed class Paper : IDisposable
    {
        private readonly MemoryStream output = new();
        private readonly SKDocument document;
        private readonly SKPaint text, bold, muted, line, fill;
        private SKCanvas canvas = null!;
        private readonly int year;
        private string title;
        private float y;
        private int page;
        private bool finished;
        private const float Left = 40, Width = 515, Bottom = 788;
        public Paper(string title, int year)
        {
            this.title = title; this.year = year;
            document = SKDocument.CreatePdf(output, new SKDocumentPdfMetadata { Title = title, Author = "GSM-Kurswahl" });
            text = new() { Typeface = SKTypeface.FromFamilyName("Arial"), TextSize = 10, IsAntialias = true, Color = new SKColor(25, 47, 61) };
            bold = new() { Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), TextSize = 14, IsAntialias = true, Color = new SKColor(0, 102, 113) };
            muted = new() { Typeface = text.Typeface, TextSize = 8, IsAntialias = true, Color = new SKColor(85, 101, 111) };
            line = new() { Color = new SKColor(190, 207, 214), StrokeWidth = .6f };
            fill = new() { Color = new SKColor(233, 244, 243) };
            NewPage();
        }
        private void NewPage()
        {
            if (page > 0) document.EndPage();
            canvas = document.BeginPage(595.28f, 841.89f); page++; canvas.Clear(SKColors.White);
            canvas.DrawText($"GSM / KURSWAHL                                      ABITUR {year}", Left, 27, muted);
            canvas.DrawLine(Left, 35, Left + Width, 35, line);
            canvas.DrawText(title + "  |  " + DateTime.Now.ToString("dd.MM.yyyy") + "  |  Seite " + page, Left, 818, muted);
            y = 62;
        }
        public void StartStudent(string label) { title = label; NewPage(); }
        public void Ensure(float height) { if (y + height > Bottom) NewPage(); }
        public void Title(string value) { bold.TextSize = 22; Paragraph(value, false, bold); bold.TextSize = 14; }
        public void Heading(string value) { Ensure(45); y += 8; Paragraph(value, false, bold); }
        public void Paragraph(string value, bool small = false, SKPaint? paint = null)
        {
            paint ??= small ? muted : text;
            foreach (var row in Wrap(value, paint, Width)) { Ensure(paint.TextSize + 5); canvas.DrawText(row, Left, y, paint); y += paint.TextSize + 5; }
            y += 6;
        }
        public void BlankLines(int count) { Ensure(count * 25); for (var i = 0; i < count; i++) { y += 25; canvas.DrawLine(Left, y - 8, Left + Width, y - 8, line); } }
        public void Signature()
        {
            Ensure(50); y += 30;
            canvas.DrawLine(Left, y, Left + 160, y, line);
            canvas.DrawLine(Left + 200, y, Left + Width, y, line);
            canvas.DrawText("Ort, Datum", Left, y + 15, muted);
            canvas.DrawText("Unterschrift Schülerin / Schüler", Left + 200, y + 15, muted);
            y += 30;
        }
        public void Table(string[] headers, string[][] rows, float[] widths)
        {
            void Header()
            {
                canvas.DrawRect(Left, y - 12, Width, 23, fill); float x = Left;
                for (var c = 0; c < headers.Length; c++) { canvas.DrawText(headers[c], x + 5, y + 2, text); x += widths[c]; }
                y += 23;
            }
            Ensure(55); Header();
            foreach (var row in rows)
            {
                var cells = row.Select((v, i) => Wrap(v, text, widths[i] - 10).ToArray()).ToArray();
                var height = Math.Max(23, cells.Max(c => c.Length) * 14 + 8);
                if (y + height > Bottom) { NewPage(); Header(); }
                float x = Left;
                for (var c = 0; c < cells.Length; c++)
                {
                    for (var l = 0; l < cells[c].Length; l++) canvas.DrawText(cells[c][l], x + 5, y + l * 14, text);
                    x += widths[c];
                }
                y += height; canvas.DrawLine(Left, y - 13, Left + Width, y - 13, line);
            }
            y += 8;
        }
        private static IEnumerable<string> Wrap(string value, SKPaint paint, float width)
        {
            var current = "";
            foreach (var word in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
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
        public byte[] Finish() { document.EndPage(); document.Close(); finished = true; return output.ToArray(); }
        public void Dispose() { if (!finished) { document.EndPage(); document.Close(); } document.Dispose(); output.Dispose(); text.Dispose(); bold.Dispose(); muted.Dispose(); line.Dispose(); fill.Dispose(); }
    }
}
