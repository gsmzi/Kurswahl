using System.Text.Json;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.Storage;

namespace GsmKurswahl.TeacherApp;

public static class DemoWorkspace
{
    public static void Prepare(string root, OfferCatalog catalog)
    {
        root = Path.GetFullPath(root);
        var marker = Path.Combine(root, "demo-80.complete");
        if (File.Exists(marker))
        {
            if (File.ReadAllText(marker) != "GSM-DEMO-80-v1" || !File.Exists(Path.Combine(root, "workspace.json")))
                throw new InvalidDataException("Unvollständiger Testbestand. Einen neuen leeren Testordner wählen.");
            return; // Reopening never resets user experiments.
        }
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new IOException("Testbestand wird nur in einem leeren Ordner angelegt; vorhandene Daten bleiben erhalten.");
        var students = DemoCohort.Create(catalog); // Validate every choice before creating the workspace.
        var workspace = new TeacherWorkspace(root); workspace.Create(catalog, "demo-beratung");
        using var store = workspace.OpenActive();
        var session = new TeacherSession(store, catalog, "demo-generator");
        var distribution = new ProfileDistribution(store);
        foreach (var row in students)
        {
            var folder = Path.Combine(root, "Schuelerdateien", row.Record.Student.ClassName, row.Record.Student.ExternalStudentId);
            Directory.CreateDirectory(folder);
            var record = row.Record with { HomeDirectory = folder, MappingConfirmed = true };
            store.SaveStudent(record, null);
            var delivery = distribution.Distribute(record, distribution.Issue(record), catalog);
            if (delivery.Status != "verteilt") throw new IOException("Demo-Verteilung fehlgeschlagen: " + delivery.Status);
            var path = Path.Combine(folder, "aktuell.json");
            var bytes = JsonDocuments.WriteChoice(row.Choice);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            var contentToken = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            var preview = session.Preview(row.Choice, row.Choice.StudentId, new(path, contentToken, "demo-80-v1", DateTimeOffset.UtcNow, ""));
            session.Confirm(preview, true, false, "Synthetischer Testbestand; strukturell geprüft, keine verbindliche Abgabe");
        }
        store.EnablePlanning();
        var baseline = CoursePlanning.Capture(store, catalog, "demo-generator", "Ausgangsstand · 80 synthetische Schüler", true);
        store.SavePlanningDocument(baseline);
        store.SavePlanningDocument(baseline with { Id = Guid.NewGuid(), Name = "Vergleich · ohne voraussichtliche Abgänger", IncludeExpectedDepartures = false });
        var scenario = CoursePlanning.PreviewCancellation(baseline, ["BIO-LF-BILI"]);
        store.SavePlanningDocument(CoursePlanning.Scenario(scenario, "Szenario · BIO LF bilingual entfällt"));
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Complete });
        var overview = new { students = students.Length, active = 68, expectedDepartures = 8, confirmedDepartures = 4,
            invalidChoices = 0, status = "needs_review", detail = "Strukturell geprüft; fachliche Jahrgangsfreigabe bleibt offen.",
            lfCombinations = CoursePlanning.Analyze(baseline).Combinations.Length };
        File.WriteAllText(Path.Combine(root, "TESTBESTAND.json"), JsonSerializer.Serialize(overview, DocumentJson.Options));
        File.WriteAllText(Path.Combine(root, "TESTBESTAND.md"), """
            # Synthetischer Testjahrgang 2029

            80 Schüler in 10a–10d, 16 LF-Kombinationen. 68 aktive Schüler,
            8 voraussichtliche und 4 bestätigte Abgänger. Planung mit voraussichtlichen
            Abgängern: 76 Personen, ohne: 68. Alle 80 Wahlen sind strukturell ohne
            Invalid-Meldungen geprüft. Needs_review bleibt wegen fachlicher Freigaben
            sichtbar; die Wahlen sind Vorwahlentwürfe, keine verbindlichen Abgaben.

            Unter Kursplanung sind zwei Ausgangsstände und ein separates Szenario
            zur Streichung von BIO LF bilingual gespeichert. Aktive Wahlen sind unverändert.
            Originalwünsche, Profile und Fachangebot liegen je Person unter Schuelerdateien.

            Wiederholter Start über Testversion-80-Schueler.cmd öffnet diesen Bestand
            unverändert; eigene Experimente werden nicht zurückgesetzt.
            """);
        File.WriteAllText(marker, "GSM-DEMO-80-v1");
    }
}
