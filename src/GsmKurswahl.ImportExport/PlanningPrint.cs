using GsmKurswahl.Domain;

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
        using var pdf = new SchoolPaper("Kontrollbogen - " + rows[0].Student.Student.ExternalStudentId, catalog.GraduationYear);
        for (var i = 0; i < rows.Length; i++)
        {
            var item = rows[i];
            if (i > 0) pdf.StartStudent("Kontrollbogen - " + item.Student.Student.ExternalStudentId);
            pdf.Title("Kontrollbogen zur Kurswahl");
            pdf.Paragraph("Stand der Lehrerverwaltung · Abitur " + catalog.GraduationYear);
            pdf.Paragraph(item.Student.Student.SyntheticDisplayName + "  |  Klasse " + item.Student.Student.ClassName + "  |  " + item.Student.Student.ExternalStudentId);
            if (item.Choice is null) { pdf.Paragraph("Für diese Person liegt noch keine Kurswahl vor."); continue; }
            var table = item.Choice.Selections.OrderBy(s => s.Type).ThenBy(s => SubjectLabel(catalog, s.SubjectId), StringComparer.CurrentCulture).Select(s =>
            {
                var hours = catalog.Offers.FirstOrDefault(o => o.Id == s.OfferId)?.Schedule.Select(h => h.WeeklyHours).Distinct().ToArray() ?? [];
                return new[] { SubjectLabel(catalog, s.SubjectId) + (s.Variant == CourseVariant.Bilingual ? " bilingual" : "")
                        + CourseAnnotations.Suffix(catalog.Offers.FirstOrDefault(o => o.Id == s.OfferId))
                        + (item.Choice.PlannedOralSubjects.Contains(s.SubjectId) && s.Type != CourseType.Lf ? "  (mündlich geplant)" : ""),
                    TypeLabel(s.Type), hours.Length == 0 ? "" : string.Join("/", hours), Mark(s, 1), Mark(s, 2), Mark(s, 3), Mark(s, 4) };
            }).ToArray();
            pdf.Heading("Belegung nach Halbjahren");
            pdf.Paragraph("x = belegt · Std. = Wochenstunden · LF = Leistungsfach · BF = Basisfach", true);
            pdf.Table(["Fach und Hinweise", "Art", "Std.", "HJ 1", "HJ 2", "HJ 3", "HJ 4"], table, [235, 65, 40, 45, 45, 45, 45]);
            var counts = Enumerable.Range(1, 4).Select(h => item.Choice.Selections.Count(s => s.Semesters.Contains(h))).ToArray();
            var weekly = Enumerable.Range(1, 4).Select(h => item.Choice.Selections.Where(s => s.Semesters.Contains(h))
                .Sum(s => catalog.Offers.FirstOrDefault(o => o.Id == s.OfferId)?.Schedule.FirstOrDefault(x => x.Semester == h)?.WeeklyHours ?? 0)).ToArray();
            pdf.Table(["Übersicht", "HJ 1", "HJ 2", "HJ 3", "HJ 4"],
                [new[] { "Kurse" }.Concat(counts.Select(n => n.ToString())).ToArray(), new[] { "Wochenstunden" }.Concat(weekly.Select(n => n.ToString())).ToArray()], [235, 60, 60, 60, 60]);
            pdf.Paragraph($"Halbjahreskurse insgesamt: {item.Choice.Selections.Sum(s => s.Semesters.Length)}", true);
            pdf.Paragraph("Geplante mündliche Prüfungsfächer (unverbindlich): " + (item.Choice.PlannedOralSubjects.IsEmpty ? "noch nicht geplant"
                : string.Join(", ", item.Choice.PlannedOralSubjects.Select(o => SubjectLabel(catalog, o)))));
            if (item.Violations.Length > 0) { pdf.Heading("Noch zu korrigieren"); foreach (var v in item.Violations) pdf.Paragraph("! " + v); }
            if (item.Hints.Length > 0) { pdf.Heading("Besonderheiten / mit der Oberstufenberatung zu klären"); foreach (var h in item.Hints) pdf.Paragraph("- " + h); }
            pdf.Ensure(100);
            pdf.Paragraph("Ich habe die oben aufgeführte Kurswahl geprüft. Die endgültige Bestätigung erfolgt auf dem ASV-Formular.", true);
            pdf.Signature();
        }
        return pdf.Finish();
        static string Mark(CourseSelection s, int h) => s.Semesters.Contains(h) ? "x" : "";
    }

    /// <summary>One line per pupil of the displayed group.</summary>
    public static byte[] Overview(int year, string filter, string[][] rows)
    {
        using var pdf = new SchoolPaper("Kurswahlen - Übersicht", year);
        pdf.Title("Kurswahlen - Übersicht");
        pdf.Paragraph(filter + $"  |  {rows.Length} Personen");
        pdf.Paragraph("LF = Leistungsfach · BF = Basisfach · mündliche Fächer sind unverbindlich geplant.", true);
        pdf.Table(["Name", "Kl.", "LF", "Basis- und Wahlfächer", "Mündl.", "Prüfstand"], rows, [100, 32, 80, 175, 50, 78]);
        return pdf.Finish();
    }

    /// <summary>Course lists, each on its own page. Rows: number, name, class, semesters.</summary>
    public static byte[] CourseLists(int year, IEnumerable<CourseListPrint> lists)
    {
        var all = lists.ToArray();
        if (all.Length == 0) throw new ArgumentException("Keine Kursliste vorhanden.");
        using var pdf = new SchoolPaper("Kursliste - " + all[0].Title, year);
        for (var i = 0; i < all.Length; i++)
        {
            if (i > 0) pdf.StartStudent("Kursliste - " + all[i].Title);
            pdf.Title(all[i].Title);
            pdf.Paragraph($"{all[i].Rows.Length} Schülerinnen und Schüler  |  Stand der Kurswahl, keine Kurszuteilung");
            pdf.Table(["Nr.", "Name", "Klasse", "Halbjahre"], all[i].Rows, [40, 265, 80, 130]);
        }
        return pdf.Finish();
    }

    public static byte[] Demand(int year, string source, string filter, string[][] rows, OfferCatalog? catalog = null)
    {
        using var pdf = new SchoolPaper("Wahlzahlen", year);
        pdf.Title("Kursplanung - Wahlzahlen");
        pdf.Paragraph(source);
        pdf.Paragraph(filter);
        pdf.Paragraph("GK/Geo: Einzelhalbjahre. Sonst gleiche Zahlen zusammengefasst; KS1/KS2 bei Abweichungen.", true);
        pdf.Table(["Fach", "LF", "LF bili", "BF", "BF bili", "Wahl"], rows, [170, 45, 48, 112, 65, 75]);
        if (catalog is not null)
        {
            var annotations = catalog.Offers.Where(o => CourseAnnotations.Label(o).Length > 0)
                .OrderBy(o => SubjectLabel(catalog, o.SubjectId), StringComparer.CurrentCulture).ThenBy(o => o.Type).ThenBy(o => o.Variant)
                .Select(o => new[] { SubjectLabel(catalog, o.SubjectId) + " " + TypeLabel(o.Type) + (o.Variant == CourseVariant.Bilingual ? " bilingual" : ""), CourseAnnotations.Label(o) }).ToArray();
            if (annotations.Length > 0)
            {
                pdf.Heading("Hinweise zu Koopkursen und Aufsetzerkursen");
                pdf.Paragraph("Die Markierungen dienen der Übersicht und ändern weder Wahlzahlen noch Belegbarkeit.", true);
                pdf.Table(["Kurs", "Markierung"], annotations, [210, 305]);
            }
        }
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
        using var pdf = new SchoolPaper("Nachwahl - " + rows[0].Remaining.StudentId, catalog.GraduationYear);
        for (var i = 0; i < rows.Length; i++)
        {
            if (i > 0) pdf.StartStudent("Nachwahl - " + rows[i].Remaining.StudentId);
            WriteReselection(pdf, rows[i].Student, rows[i].Remaining, catalog, rows[i].Removed, rows[i].Notes);
        }
        return pdf.Finish();
    }

    private static void WriteReselection(SchoolPaper pdf, StudentRecord student, CourseChoice remaining, OfferCatalog catalog, string[] removed, string[] notes)
    {
        pdf.Title("Nachwahl zur Kurswahl");
        pdf.Paragraph(student.Student.SyntheticDisplayName + "  |  Klasse " + student.Student.ClassName);
        pdf.Heading(removed.Length > 0 ? "Diese gewählten Kurse entfallen" : "Bitte ergänze oder ändere deine Wahl");
        foreach (var id in removed)
        {
            var offer = catalog.Offers.Single(o => o.Id == id);
            var type = offer.Type switch { CourseType.Lf => "LF", CourseType.Bf => "BF", CourseType.Elective => "Wahlfach", _ => "Seminarkurs" };
            pdf.Paragraph((catalog.Subjects.Single(s => s.Id == offer.SubjectId).Name) + " - " + type
                + (offer.Variant == CourseVariant.Bilingual ? " bilingual" : "") + CourseAnnotations.Suffix(offer));
        }
        foreach (var note in notes) pdf.Paragraph("- " + note);
        pdf.Paragraph("Bitte trage deine Nachwahl oder Umwahl unten ein und gib diesen Bogen unterschrieben bei der Oberstufenberatung ab. Die neue Wahl wird anschließend geprüft.");
        pdf.Heading("Deine verbleibenden Kurse");
        var rows = remaining.Selections.OrderBy(s => s.Type).ThenBy(s => s.SubjectId).Select(s => new[] {
            catalog.Subjects.Single(x => x.Id == s.SubjectId).Name + (s.Variant == CourseVariant.Bilingual ? " bilingual" : "") + CourseAnnotations.Suffix(catalog.Offers.FirstOrDefault(o => o.Id == s.OfferId)),
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

}
