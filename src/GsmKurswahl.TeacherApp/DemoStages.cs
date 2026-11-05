using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.ImportExport;
using GsmKurswahl.Storage;

namespace GsmKurswahl.TeacherApp;

public sealed record DemoStage(string Id, string Title, string Description);

/// <summary>
/// Named synthetic states for the test package. Each is generated on the tester's computer, because the teacher
/// database stores absolute pupil folders. The teacher stages go through the same import and distribution services
/// as the real workflow. Reopening never resets a stage; only deleting its folder does.
/// </summary>
public static class DemoStages
{
    public const string ImportFile = "schuelerliste-80.csv";
    public const string DistributionFolder = "Austeilordner";
    public const string DescriptionFile = "TESTSTAND.txt";
    public const int SubmittedBeforeCollection = 60;
    private const string MarkerFile = "teststand.complete";
    private const string MarkerVersion = "GSM-TESTSTAND-v1";
    private const string Actor = "demo-beratung";

    public static readonly ImmutableArray<DemoStage> All =
    [
        new("lehrer-1-anfang", "Lehrer 1 · Ganz am Anfang", $"Noch kein Jahrgang eingerichtet. Der Einrichtungsassistent startet. Die Schülerliste {ImportFile} und die leeren Klassenordner unter {DistributionFolder} liegen in diesem Ordner bereit."),
        new("lehrer-2-importiert", "Lehrer 2 · Schüler importiert", $"80 Schüler sind importiert, die Dateien aber noch nicht verteilt. Als Grundverzeichnis ist {DistributionFolder} vorbelegt."),
        new("lehrer-3-ausgeteilt", "Lehrer 3 · Ausgeteilt, noch keine Wahlen", "Profile und Fachangebot liegen in allen 80 Schülerordnern. Es ist noch keine Kurswahl vorhanden."),
        new("lehrer-4-wahlen-abgegeben", "Lehrer 4 · Wahlen liegen zum Einsammeln bereit", $"{SubmittedBeforeCollection} von 80 Schülern haben gewählt, im Lehrerbestand ist noch nichts angekommen. Das Lehrerprogramm liest die Wahlen beim Öffnen automatisch ein."),
        new("lehrer-5-eingesammelt", "Lehrer 5 · Wahlen eingesammelt", "Alle 80 Wahlen sind im Lehrerbestand. Unter Kursplanung liegen zwei Ausgangsstände und das Szenario „BIO LF bilingual entfällt“."),
        new("lehrer-6-nachwahl", "Lehrer 6 · Kurs gestrichen, Nachwahl offen", "Wie Lehrer 5, aber BIO LF bilingual ist verbindlich gestrichen. Betroffene Schüler haben neue Revisionen und, wo nötig, einen Nachwahlvermerk."),
        new("schueler-1-blanko", "Schüler 1 · Blanko", "Profil und Fachangebot sind da, es gibt noch keine Wahl."),
        new("schueler-2-angefangen", "Schüler 2 · Wahl angefangen", "Drei Leistungsfächer sind gewählt, der Rest fehlt noch."),
        new("schueler-3-fertig", "Schüler 3 · Wahl fertig", "Eine vollständige Kurswahl ist gespeichert."),
        new("schueler-4-alle-voraussetzungen", "Schüler 4 · Blanko mit Bili, Informatik und Italienisch", "Keine Wahl, aber alle besonderen Voraussetzungen sind bestätigt."),
        new("schueler-5-sportbefreit", "Schüler 5 · Blanko mit Sportbefreiung", "Keine Wahl, Sportbefreiung in Halbjahr 3 und 4 (Verwaltungsmerkmal ohne Gesundheitsangaben).")
    ];

    public static DemoStage Find(string id) => All.SingleOrDefault(s => s.Id == id)
        ?? throw new ArgumentException("Unbekannter Teststand: " + id);

    public static void Prepare(string id, string root, OfferCatalog catalog)
    {
        var stage = Find(id);
        root = Path.GetFullPath(root);
        var marker = Path.Combine(root, MarkerFile);
        var expected = MarkerVersion + ":" + id;
        if (File.Exists(marker))
        {
            if (File.ReadAllText(marker) != expected)
                throw new InvalidDataException("Dieser Teststand stammt aus einem anderen Testpaket. Bitte „Alles zuruecksetzen“ ausführen.");
            return; // Reopening never resets a tester's experiments.
        }
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new IOException("Teststand ist unvollständig oder der Ordner wird anders genutzt. Bitte „Alles zuruecksetzen“ ausführen.");
        var students = DemoCohort.Create(catalog);
        Directory.CreateDirectory(root);
        if (id.StartsWith("lehrer-", StringComparison.Ordinal)) PrepareTeacher(All.IndexOf(stage) + 1, root, catalog, students);
        else PreparePupil(id, root, catalog, students[1]);
        File.WriteAllText(Path.Combine(root, DescriptionFile), stage.Title + Environment.NewLine + Environment.NewLine + stage.Description
            + Environment.NewLine + Environment.NewLine + "Alle Personen sind synthetisch. Keine echten Schülerdaten eintragen." + Environment.NewLine);
        File.WriteAllText(marker, expected);
    }

    private static void PrepareTeacher(int level, string root, OfferCatalog catalog, ImmutableArray<DemoStudent> students)
    {
        var folders = Directory.CreateDirectory(Path.Combine(root, DistributionFolder)).FullName;
        foreach (var row in students) Directory.CreateDirectory(Path.Combine(folders, row.Record.Student.ClassName, FolderName(row)));
        var csv = ImportTable(students);
        File.WriteAllText(Path.Combine(root, ImportFile), csv);
        // Without this file a published teacher program would preselect the real school drive.
        File.WriteAllBytes(Path.Combine(root, SchoolDefaults.FileName), JsonSerializer.SerializeToUtf8Bytes(new SchoolDefaults(1, folders, ""), DocumentJson.Options));
        if (level == 1) return;

        var workspace = new TeacherWorkspace(root); workspace.Create(catalog, Actor);
        using var store = workspace.OpenActive();
        Import(store, csv);
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Mapping });
        if (level == 2) return;

        var homes = Distribute(store, folders, catalog);
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Complete });
        if (level == 3) return;

        var session = new TeacherSession(store, catalog, "demo-generator");
        foreach (var row in level == 4 ? students.Take(SubmittedBeforeCollection) : students)
        {
            var path = DemoWorkspace.WriteChoice(homes[row.Choice.StudentId], row.Choice);
            if (level >= 5) DemoWorkspace.Collect(session, row.Choice, path);
        }
        if (level == 4) return;

        var baseline = DemoWorkspace.SavePlanning(store, catalog);
        if (level == 6) CoursePlanning.Confirm(store, CoursePlanning.PreviewCancellation(baseline, ["BIO-LF-BILI"]), Actor, "", true);
    }

    private static void Import(SqliteAdministrationStore store, string csv)
    {
        var table = StudentTableImport.Csv(csv);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(csv)));
        var origin = new ImportOrigin(ImportFile, hash, "m3-table-v1:teststand", DateTimeOffset.UtcNow, Actor);
        foreach (var row in StudentTableImport.Preview(table, table.Headers.ToDictionary(h => h, h => h), origin, Actor))
        {
            if (row.Candidate is null) throw new InvalidDataException($"Zeile {row.Row} der Schülerliste: {row.Error}");
            StudentImportService.Confirm(store, row.Candidate, null, true);
        }
    }

    // Same steps as the teacher's joint distribution, without copying the ~100 MB pupil program 80 times.
    private static Dictionary<string, string> Distribute(SqliteAdministrationStore store, string folders, OfferCatalog catalog)
    {
        var distribution = new ProfileDistribution(store);
        var homes = new Dictionary<string, string>();
        foreach (var record in store.Students())
        {
            var confirmed = record with { HomeDirectory = StudentDirectories.Resolve(folders, record.HomeDirectory), MappingConfirmed = true,
                Version = record.Version + 1, Actor = Actor, ChangedAt = DateTimeOffset.UtcNow };
            store.SaveStudent(confirmed, record.Version);
            var delivery = distribution.Distribute(confirmed, distribution.Issue(confirmed), catalog);
            if (delivery.Status != "verteilt") throw new IOException("Verteilung fehlgeschlagen: " + delivery.Status);
            homes.Add(record.Student.ExternalStudentId, confirmed.HomeDirectory);
        }
        return homes;
    }

    private static void PreparePupil(string id, string root, OfferCatalog catalog, DemoStudent row)
    {
        var prerequisites = row.Record.Student.Prerequisites;
        if (id == "schueler-4-alle-voraussetzungen")
            prerequisites = prerequisites with { ItalianProfile = Knowledge.Yes, InformaticsBridge = Knowledge.Yes, InformaticsImp = Knowledge.Yes, ContinuousBilingual = Knowledge.Yes };
        if (id == "schueler-5-sportbefreit") prerequisites = prerequisites with { SportExemptSemesters = [3, 4] };
        var data = Directory.CreateDirectory(StudentFolderLayout.Data(root)).FullName;
        var profile = new StudentProfile(SchemaVersions.Profile, 1, row.Choice.StudentId, "", catalog.GraduationYear, DateTimeOffset.UtcNow, prerequisites)
        { DisplayName = row.Record.Student.SyntheticDisplayName, ClassName = row.Record.Student.ClassName };
        File.WriteAllBytes(Path.Combine(data, "profil.json"), JsonSerializer.SerializeToUtf8Bytes(profile, DocumentJson.Options));
        File.WriteAllBytes(Path.Combine(data, "fachangebot.json"), JsonSerializer.SerializeToUtf8Bytes(catalog, DocumentJson.Options));
        if (id == "schueler-3-fertig") DemoWorkspace.WriteChoice(root, row.Choice);
        if (id == "schueler-2-angefangen")
            DemoWorkspace.WriteChoice(root, row.Choice with { Selections = [.. row.Choice.Selections.Where(s => s.Type == CourseType.Lf)], PlannedOralSubjects = [] });
    }

    private static string LastName(DemoStudent row) => "Testperson" + row.Choice.StudentId["demo-".Length..];
    private static string FolderName(DemoStudent row) => LastName(row) + ".Demo";

    private static string ImportTable(ImmutableArray<DemoStudent> students)
    {
        static string Known(Knowledge value) => value switch { Knowledge.Yes => "ja", Knowledge.No => "nein", _ => "" };
        var text = new StringBuilder("studentId;firstName;lastName;class;year;italian;bridge;imp;bilingual;religion;sport;languages;religionPermission;religionExamPermission;permanentSport;departure\n");
        foreach (var row in students)
        {
            var student = row.Record.Student; var prior = student.Prerequisites;
            var departure = student.Departure switch { DepartureStatus.ExpectedDeparture => "voraussichtlich", DepartureStatus.ConfirmedDeparture => "bestaetigt", _ => "aktiv" };
            string[] cells = [student.ExternalStudentId, "Demo", LastName(row), student.ClassName, student.GraduationYear.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Known(prior.ItalianProfile), Known(prior.InformaticsBridge), Known(prior.InformaticsImp), Known(prior.ContinuousBilingual), prior.ReligionEthicsGrade10 ?? "",
                "keine", string.Join(",", prior.ContinuedLanguages), Known(prior.ReligionEthicsPermission), Known(prior.ReligionEthicsExamPermission), Known(prior.PermanentSportExemption), departure];
            text.AppendJoin(';', cells).Append('\n');
        }
        return text.ToString();
    }
}
