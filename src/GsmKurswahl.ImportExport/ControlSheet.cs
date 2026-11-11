using GsmKurswahl.Domain;

namespace GsmKurswahl.ImportExport;

/// <summary>Direct offline PDF export. All rule decisions are supplied by the application.</summary>
public static class ControlSheet
{
    public static byte[] Render(CourseChoice choice, OfferCatalog catalog, ProfileContext profile,
        ValidationResult validation, IEnumerable<SemesterSummary> summaries, IEnumerable<ExamOption> exams)
    {
        using var pdf = new SchoolPaper("Entwurf · " + choice.StudentId, choice.GraduationYear);
        pdf.Title("Kontrollbogen zur Kurswahl");
        pdf.Paragraph("Vorläufiger Entwurf · nicht eingereicht · keine verbindliche Freigabe");
        pdf.Paragraph(string.IsNullOrWhiteSpace(profile.DisplayName) ? "Schüler-ID: " + choice.StudentId
            : profile.DisplayName + " · Klasse " + profile.ClassName + " · " + choice.StudentId);
        pdf.Paragraph($"Wahlstand: {choice.UpdatedAt:dd.MM.yyyy HH:mm zzz} · Revision {choice.Revision}", true);
        pdf.Heading("Deine Belegung nach Halbjahren");
        pdf.Paragraph("In den Halbjahresspalten stehen die Wochenstunden; ein Strich bedeutet keine Belegung.", true);
        var rows = choice.Selections.OrderBy(s => s.Type).ThenBy(s => Name(s.SubjectId), StringComparer.CurrentCulture).Select(s =>
        {
            var offer = catalog.Offers.FirstOrDefault(o => o.Id == s.OfferId);
            return new[] { Name(s.SubjectId) + (s.Variant == CourseVariant.Bilingual ? " bilingual" : "") + CourseAnnotations.Suffix(offer),
                Type(s.Type), Hours(1), Hours(2), Hours(3), Hours(4) };
            string Hours(int h) => s.Semesters.Contains(h) ? offer?.Schedule.FirstOrDefault(x => x.Semester == h)?.WeeklyHours.ToString() ?? "?" : "-";
        }).ToArray();
        pdf.Table(["Fach und Hinweise", "Kursart", "HJ 1", "HJ 2", "HJ 3", "HJ 4"], rows, [235, 60, 48, 48, 48, 48]);
        pdf.Heading("Kurse und Wochenstunden");
        var totals = summaries.OrderBy(s => s.Semester).ToArray();
        pdf.Table(new[] { "Übersicht" }.Concat(totals.Select(s => "HJ " + s.Semester)).ToArray(),
            [new[] { "Kurse" }.Concat(totals.Select(s => s.CourseCount.ToString())).ToArray(), new[] { "Wochenstunden" }.Concat(totals.Select(s => s.WeeklyHours.ToString())).ToArray()],
            new[] { 235f }.Concat(totals.Select(_ => 60f)).ToArray());
        pdf.Paragraph("Belegung und spätere Anrechnung sind getrennt: mindestens 42 belegte Kurse; höchstens 40 Pflichtanrechnungskurse.", true);
        pdf.Heading("Deine mündliche Prüfungsplanung");
        pdf.Paragraph(choice.PlannedOralSubjects.IsEmpty ? "Noch keine mündlichen Prüfungsfächer geplant."
            : "Unverbindlich geplant: " + string.Join(", ", choice.PlannedOralSubjects.Select(Name)));
        var options = exams.ToArray();
        if (options.Length == 0) pdf.Paragraph("Keine passende Kombination aus der aktuellen Belegung ermittelt. Offene Punkte prüfen.");
        else
        {
            pdf.Ensure(100);
            pdf.Paragraph("Mögliche Kombinationen aus deiner Belegung (vorläufig):");
            pdf.Table(["Erstes Fach", "Zweites Fach", "Mindestanrechnung"], options.Select(o => new[] { Name(o.First), Name(o.Second), o.MinimumCredits + " Kurse" }).ToArray(), [170, 170, 140]);
            pdf.Paragraph("Alle Kombinationen sind vorläufig. Voraussetzungen, Rechtsstand und Anrechnung sind zu bestätigen. BLL/SIA ersetzt kein Fach automatisch.", true);
        }
        pdf.Heading("Was noch zu tun ist");
        pdf.Paragraph(validation.Status == ReviewStatus.Invalid ? "Die Kurswahl muss korrigiert werden."
            : "Die Kurswahl bleibt vorläufig und muss von der Oberstufenberatung geprüft werden.");
        foreach (var group in validation.Messages.OrderBy(m => m.Rule.Severity == ReviewStatus.Invalid ? 0 : 1).GroupBy(m => m.Rule.Severity == ReviewStatus.Invalid))
        {
            pdf.Heading(group.Key ? "Noch zu korrigieren" : "Mit der Oberstufenberatung klären");
            foreach (var message in group)
            {
                pdf.Ensure(65);
                pdf.Paragraph(message.Explanation);
                pdf.Paragraph("Nächster Schritt: " + message.Remedy + " (" + message.Rule.Id + ")", true);
            }
        }
        pdf.Heading("Grundlage dieses Entwurfs");
        pdf.Paragraph($"Angebot {choice.OfferVersion} · Regelstand {choice.RuleVersion}", true);
        var status = profile.Status switch { ProfileStatus.Verified => "geladen, verbindliche Lehrerprüfung steht aus", ProfileStatus.ProfileInvalid => "ungültig", ProfileStatus.Missing => "fehlt", _ => "ungeprüft" };
        pdf.Paragraph($"Persönliches Profil: {status} · Revision {profile.Revision?.ToString() ?? "unbekannt"}", true);
        pdf.Paragraph("Die Oberstufenberatung prüft gegen den aktuellen geschützten Stammdatenbestand, das Angebot und den Rechtsstand.", true);
        return pdf.Finish();
        string Name(string id) => catalog.Subjects.FirstOrDefault(s => s.Id == id)?.Name ?? id;
    }
    internal static string Type(CourseType type) => type switch { CourseType.Lf => "LF", CourseType.Bf => "BF", CourseType.Elective => "Wahlfach", _ => "Seminar" };
}
