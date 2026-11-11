using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;
using GsmKurswahl.Tests;
using Microsoft.Data.Sqlite;

namespace GsmKurswahl.Storage.Tests;

/// <summary>
/// Frozen file and database contracts. "current" fixtures pin the exact shape the program writes today: a renamed
/// property, enum value or type fails here. "legacy" fixtures are historical files that must stay readable forever.
/// Never edit a legacy fixture; a structural change needs a new schema version, an upgrade and new fixtures.
/// </summary>
public sealed class CompatibilityTests : IDisposable
{
    private static string Fixtures([CallerFilePath] string source = "") => Path.Combine(Path.GetDirectoryName(source)!, "Compatibility");
    private static readonly Guid G1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid G2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid G3 = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-02T08:00:00Z");
    private readonly string root = Path.Combine(Path.GetTempPath(), "gsm-compat-" + Guid.NewGuid());
    public CompatibilityTests() => Directory.CreateDirectory(root);
    public void Dispose() { SqliteConnection.ClearAllPools(); try { Directory.Delete(root, true); } catch (IOException) { } }

    private static PriorEducation Prior => new(Knowledge.Yes, Knowledge.No, Knowledge.Unknown, Knowledge.Yes, "KR", [1])
    { ContinuedLanguages = ["E", "F"], ReligionEthicsPermission = Knowledge.Yes, ReligionEthicsExamPermission = Knowledge.No, PermanentSportExemption = Knowledge.No };
    private static OfferCatalog Catalog => new(SchemaVersions.Catalog, "demo-2029-compat", 2029, true,
        [new("D", "Deutsch"), new("NEU", "Neues Fach") { TaskField = 3, IsForeignLanguage = false, IsScience = true }],
        [TestData.Offer("D-LF", "D", CourseType.Lf) with { CooperationSchool = CooperationSchool.Ewg, IsTopUpCourse = true }, TestData.Offer("NEU-BF", "NEU") with { Variant = CourseVariant.Bilingual, Status = OfferStatus.Planned, Requirements = [Requirement.ContinuousBilingual] }])
    { SubmissionDeadline = new DateOnly(2027, 3, 15) };
    private static CourseChoice Choice => new(SchemaVersions.Choice, G1, "demo-001", 2029, "demo-2029-compat", "m2-2", 3, T, T.AddHours(1),
        SubmissionStatus.Draft, ElectionPhase.Preliminary, [new("D-LF", "D", CourseType.Lf, CourseVariant.Normal, [1, 2, 3, 4])], ["D", "NEU"]);
    private static StudentRecord Record => new(SchemaVersions.StudentRecord,
        new(G2, "demo-001", "Demo Eins", "10a", 2029, DepartureStatus.ExpectedDeparture, Prior) { FirstName = "Demo", LastName = "Eins" },
        "DEMO\\eins", "", @"C:\Demo\10a\Eins.Demo", true, 2, "demo-beratung", T, new("klasse10.csv", "ABC", "map-1", T, "DEMO\\beratung"));
    private static StudentProfile Profile => new(SchemaVersions.Profile, 2, "demo-001", "DEMO\\eins", 2029, T, Prior) { DisplayName = "Demo Eins", ClassName = "10a" };
    private static ChoiceRevision Revision => new(G3, G1, "demo-001", T, "demo-beratung", ActorRole.Teacher, RevisionSource.TeacherEdit, "Korrektur",
        "demo-2029-compat", "m2-2", Choice, [new("D-LF", [], [1, 2, 3, 4])], new("aktuell.json", "ABC", "m3-choice-v1", T, "DEMO\\eins"));
    private static TeacherYear Year => new(Catalog, @"C:\Demo\Abitur-2029\verwaltung.db", YearSetupStep.Complete, "demo-beratung")
    { SubmissionDeadline = new DateOnly(2027, 3, 15), StudentProgramDirectory = @"C:\Demo\Programm" };

    public static TheoryData<string> CurrentNames() => ["choice-v2", "catalog-v3", "profile-v2", "student-record-v2", "revision", "issued-profile",
        "distribution-entry", "planning-document", "transfer-receipt", "collected-file", "program-delivery", "student-deletion", "workspace-v2", "transfer-v2"];
    private static object Current(string name) => name switch
    {
        "choice-v2" => Choice,
        "catalog-v3" => Catalog,
        "profile-v2" => Profile,
        "student-record-v2" => Record,
        "revision" => Revision,
        "issued-profile" => new IssuedProfile(Profile, [1, 2, 3], []),
        "distribution-entry" => new DistributionEntry(G1, "demo-001", 2, T, "verteilt", "Einzelprofil verteilt"),
        "planning-document" => new PlanningDocument(1, G1, T, "Ausgangsstand", "demo-beratung", "m2-2", Catalog, [Record], [Revision],
            [new("demo-001", true, T.AddDays(7), "Nachwahl")], true, 20, true, G2),
        "transfer-receipt" => new TransferReceipt(1, G1, "demo-001", G3, 2, "demo-2029-compat", "m2-2", T, "demo-beratung", true, "eingegeben"),
        "collected-file" => new CollectedFile(1, G1, "demo-001", T, "demo-beratung", "aktuell.json", StudentFolderCleanup.Token([123, 125]), [123, 125]),
        "program-delivery" => new ProgramDelivery(1, G1, "demo-001", T, "Kurswahl-Programm", [new("GsmKurswahl.StudentApp.exe", "AB")]),
        "student-deletion" => new StudentDeletion(1, G1, G2, T, "demo-beratung", "Schulwechsel", 7, 1),
        "workspace-v2" => new TeacherWorkspaceDocument(TeacherWorkspace.CurrentSchema, 2029, [Year]),
        "transfer-v2" => new TransferManifest(TeacherWorkspace.CurrentSchema, T, Year with { DatabasePath = "verwaltung.db" }, new string('A', 64)),
        _ => throw new ArgumentException(name)
    };

    [Theory, MemberData(nameof(CurrentNames))]
    public void CurrentWrittenShapeIsFrozenAndReadable(string name)
    {
        var value = Current(name);
        var written = JsonSerializer.Serialize(value, value.GetType(), DocumentJson.Options);
        var path = Path.Combine(Fixtures(), "current", name + ".json");
        if (!File.Exists(path) && Environment.GetEnvironmentVariable("GSM_ACCEPT_FIXTURES") == "1")
        { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, written + "\n", new UTF8Encoding(false)); }
        var frozen = File.ReadAllText(path);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(frozen), JsonNode.Parse(written)),
            $"Gespeicherte Struktur von {name} hat sich geändert. Neue Schemaversion mit Umwandlung anlegen statt die Datei anzupassen.\n{written}");
        var reread = JsonSerializer.Deserialize(frozen, value.GetType(), DocumentJson.Options)!;
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(frozen), JsonNode.Parse(JsonSerializer.Serialize(reread, value.GetType(), DocumentJson.Options))));
    }

    [Fact]
    public void PersistedEnumNamesAreFrozen()
    {
        static string Names<TEnum>() where TEnum : struct, Enum =>
            string.Join(",", Enum.GetValues<TEnum>().Select(v => JsonSerializer.Serialize(v, DocumentJson.Options).Trim('"')));
        Assert.Equal("lf,bf,elective,seminar", Names<CourseType>());
        Assert.Equal("none,ewg,gsm", Names<CooperationSchool>());
        Assert.Equal("normal,bilingual", Names<CourseVariant>());
        Assert.Equal("planned,offered,cancelled,established", Names<OfferStatus>());
        Assert.Equal("unknown,yes,no", Names<Knowledge>());
        Assert.Equal("draft,submitted", Names<SubmissionStatus>());
        Assert.Equal("preliminary,final", Names<ElectionPhase>());
        Assert.Equal("active,expected_departure,confirmed_departure", Names<DepartureStatus>());
        Assert.Equal("student,teacher,system", Names<ActorRole>());
        Assert.Equal("student_submission,teacher_edit,offer_cancellation,asv_reconciliation,restoration", Names<RevisionSource>());
        Assert.Equal("informatics,italian_profile,continuous_bilingual", Names<Requirement>());
        Assert.Equal("offer,import,mapping,review,complete", Names<YearSetupStep>());
    }

    private static byte[] Legacy(string name) => File.ReadAllBytes(Path.Combine(Fixtures(), "legacy", name));

    [Fact]
    public async Task LegacyChoiceV1LoadsAsV2WithoutTouchingTheFile()
    {
        var path = Path.Combine(root, "aktuell.json");
        await File.WriteAllBytesAsync(path, Legacy("choice-v1.json"));
        var loaded = await new JsonChoiceStore(path).LoadAsync();
        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.UpgradedFromSchema);
        Assert.Equal(SchemaVersions.Choice, loaded.Choice.SchemaVersion);
        Assert.Empty(loaded.Choice.PlannedOralSubjects);
        Assert.Equal("D-LF", Assert.Single(loaded.Choice.Selections).OfferId);
        Assert.Equal(Legacy("choice-v1.json"), await File.ReadAllBytesAsync(path));
        // Saving deliberately writes the current version and keeps the old file as backup.
        var saved = await new JsonChoiceStore(path).SaveAsync(loaded.Choice with { Revision = loaded.Choice.Revision + 1 }, loaded.ContentToken);
        Assert.Equal(SchemaVersions.Choice, JsonDocuments.SchemaVersionOf(await File.ReadAllBytesAsync(path)));
        Assert.Equal(Legacy("choice-v1.json"), await File.ReadAllBytesAsync(path + ".bak"));
        Assert.Null((await new JsonChoiceStore(path).LoadAsync())!.UpgradedFromSchema);
        Assert.Equal(saved.ContentToken, (await new JsonChoiceStore(path).LoadAsync())!.ContentToken);
    }

    [Fact]
    public async Task PupilAppLoadsLegacyDraftAndSavesCurrentFormatOnlyDeliberately()
    {
        var path = Path.Combine(root, "aktuell.json");
        await File.WriteAllBytesAsync(path, Legacy("choice-v1.json"));
        var session = new StudentSession(Catalog with { Version = "demo-2029-compat" }, "demo-001", new JsonChoiceStore(path));
        await session.LoadAsync();
        Assert.True(session.IsDirty);
        Assert.Contains("älterem Dateiformat (Version 1)", session.Message);
        Assert.Equal(Legacy("choice-v1.json"), await File.ReadAllBytesAsync(path));
        await session.SaveAsync();
        Assert.Equal(SchemaVersions.Choice, JsonDocuments.SchemaVersionOf(await File.ReadAllBytesAsync(path)));
    }

    [Fact]
    public void LegacyCatalogAndProfileV1AreUpgradedInMemory()
    {
        var catalog = JsonDocuments.ReadCatalog(Legacy("catalog-v1.json"));
        Assert.Equal(SchemaVersions.Catalog, catalog.SchemaVersion);
        Assert.Null(catalog.SubmissionDeadline);
        var profile = new SignedProfileReader().Verify(Legacy("profile-v1.json"), "demo-001", 2029);
        Assert.Equal(ProfileStatus.Verified, profile.Status);
        Assert.Equal(Knowledge.Yes, profile.Prerequisites.InformaticsImp);
        Assert.Equal("", profile.DisplayName);
    }

    [Fact]
    public void NewerVersionsAreRejectedWithoutChange()
    {
        var future = Encoding.UTF8.GetString(Legacy("catalog-v1.json")).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99");
        Assert.Throws<JsonException>(() => JsonDocuments.ReadCatalog(Encoding.UTF8.GetBytes(future)));
        Assert.Equal(ProfileStatus.ProfileInvalid, new SignedProfileReader().Verify(
            Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Legacy("profile-v1.json")).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 3")), "demo-001", 2029).Status);
        Directory.CreateDirectory(Path.Combine(root, "ws"));
        File.WriteAllText(Path.Combine(root, "ws", "workspace.json"), "{\"schemaVersion\":3,\"activeYear\":null,\"years\":[]}");
        Assert.Throws<InvalidDataException>(() => new TeacherWorkspace(Path.Combine(root, "ws")));
    }

    [Fact]
    public void LegacyWorkspaceV1OpensAndIsRewrittenOnlyOnChange()
    {
        var directory = Path.Combine(root, "ws"); Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "Abitur-2029", "verwaltung.db");
        using (new SqliteAdministrationStore(database)) { }
        var legacy = Encoding.UTF8.GetString(Legacy("workspace-v1.json")).Replace("C:\\\\Demo\\\\Abitur-2029\\\\verwaltung.db", database.Replace("\\", "\\\\"));
        File.WriteAllText(Path.Combine(directory, "workspace.json"), legacy);
        var workspace = new TeacherWorkspace(directory);
        Assert.Equal(2029, workspace.Active!.Catalog.GraduationYear);
        Assert.Equal(SchemaVersions.Catalog, workspace.Active.Catalog.SchemaVersion);
        Assert.Null(workspace.Active.SubmissionDeadline);
        Assert.Equal(legacy, File.ReadAllText(Path.Combine(directory, "workspace.json")));
        workspace.Update(workspace.Active with { SubmissionDeadline = new DateOnly(2027, 3, 1) });
        Assert.Equal(TeacherWorkspace.CurrentSchema, JsonDocuments.SchemaVersionOf(File.ReadAllBytes(Path.Combine(directory, "workspace.json"))));
        Assert.Equal(new DateOnly(2027, 3, 1), new TeacherWorkspace(directory).Active!.SubmissionDeadline);
    }

    [Fact]
    public void LegacyTransferManifestV1StaysImportable()
    {
        var manifest = JsonSerializer.Deserialize<TransferManifest>(
            typeof(TeacherWorkspace).GetMethod("UpgradeYears", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .Invoke(null, [Legacy("transfer-v1.json"), "Transportdatei"]) as byte[], DocumentJson.Options)!;
        Assert.Equal(TeacherWorkspace.CurrentSchema, manifest.SchemaVersion);
        Assert.Equal("verwaltung.db", manifest.Year.DatabasePath);
        Assert.Equal(SchemaVersions.Catalog, manifest.Year.Catalog.SchemaVersion);
    }

    [Fact]
    public void LegacyLocationV1IsReadable()
    {
        var settings = Path.Combine(root, "settings"); Directory.CreateDirectory(settings);
        File.WriteAllText(Path.Combine(settings, "location.json"), Encoding.UTF8.GetString(Legacy("location-v1.json")).Replace("C:\\\\Demo\\\\Lehrerverwaltung", root.Replace("\\", "\\\\")));
        Assert.Equal(root, new TeacherWorkspaceLocation(settings).Load());
    }

    private string LegacyDatabase()
    {
        var path = Path.Combine(root, "verwaltung-v3.db");
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = File.ReadAllText(Path.Combine(Fixtures(), "legacy", "administration-v3.sql"));
            command.ExecuteNonQuery();
        }
        return path;
    }

    [Fact]
    public void LegacyDatabaseV3IsUpgradedWithoutChangingExistingRows()
    {
        var path = LegacyDatabase();
        using (var store = new SqliteAdministrationStore(path))
        {
            var student = Assert.Single(store.Students());
            Assert.Equal(SchemaVersions.StudentRecord, student.SchemaVersion);
            Assert.Equal("", student.Student.FirstName);
            Assert.Equal(2, store.StudentHistory("demo-001").Length);
            var revision = Assert.Single(store.History("demo-001"));
            Assert.Equal(SchemaVersions.Choice, revision.Snapshot.SchemaVersion);
            Assert.True(store.Reselection("demo-001").Required);
            Assert.Equal(SchemaVersions.Profile, store.LatestProfile("demo-001")!.Profile.SchemaVersion);
            Assert.Equal("verteilt", Assert.Single(store.DistributionLog()).Status);
            var planning = Assert.Single(store.PlanningDocuments());
            Assert.Equal(SchemaVersions.Catalog, planning.Catalog.SchemaVersion);
            Assert.Equal("demo-2029-plan", store.ActivePlanningCatalog()!.Version);
            Assert.Single(store.TransferReceipts());
            Assert.Empty(store.CollectedFiles("demo-001"));
            Assert.Empty(store.Deletions());
            // Current rules judge legacy revisions without a version-mismatch finding.
            var session = new TeacherSession(store, TestData.WorkflowCatalog with { Version = "demo-2029-neu" }, "demo-beratung");
            Assert.DoesNotContain(session.Overview().Single().Validation!.Messages, m => m.Rule.Id == "OFFER-VERSION");
        }
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version; ";
        Assert.Equal(SqliteAdministrationStore.CurrentSchema, Convert.ToInt32(command.ExecuteScalar()));
        command.CommandText = "SELECT body FROM students WHERE version=1";
        Assert.Contains("\"schemaVersion\":1", ((string)command.ExecuteScalar()!).Replace(" ", ""));
    }

    [Fact]
    public void DeletingAStudentRemovesEveryTraceExceptAPersonFreeLogEntry()
    {
        var path = LegacyDatabase();
        using var store = new SqliteAdministrationStore(path);
        var internalId = store.Students().Single().Student.Id;
        Assert.Throws<InvalidOperationException>(() => store.DeleteStudent("demo-001", Guid.NewGuid(), "demo-beratung", "Test"));
        store.ArchiveCollectedFiles([new(1, Guid.NewGuid(), "demo-001", T, "demo-beratung", "aktuell.json", StudentFolderCleanup.Token([1]), [1])]);
        var log = store.DeleteStudent("demo-001", internalId, "demo-beratung", "Schulwechsel");
        Assert.Empty(store.Students()); Assert.Empty(store.StudentHistory("demo-001")); Assert.Empty(store.History("demo-001"));
        Assert.False(store.Reselection("demo-001").Required); Assert.Null(store.LatestProfile("demo-001"));
        Assert.Empty(store.DistributionLog()); Assert.Empty(store.TransferReceipts()); Assert.Empty(store.CollectedFiles("demo-001"));
        var planning = Assert.Single(store.PlanningDocuments());
        Assert.Empty(planning.Students); Assert.Empty(planning.Choices);
        Assert.Equal(1, log.UpdatedPlanningSnapshots);
        Assert.Equal(internalId, Assert.Single(store.Deletions()).InternalStudentId);
        var logJson = JsonSerializer.Serialize(store.Deletions(), DocumentJson.Options);
        Assert.DoesNotContain("demo-001", logJson); Assert.DoesNotContain("Demo", logJson.Replace("demo-beratung", ""));
        // Append-only protection is active again afterwards.
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open(); using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM deletions";
        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    [Fact]
    public void ProgramDeliveryAndFinalCollectionArchiveFirstAndRemoveOnlyOwnFiles()
    {
        using var store = new SqliteAdministrationStore(Path.Combine(root, "admin.db"));
        var home = Path.Combine(root, "home"); Directory.CreateDirectory(home);
        var record = Record with { HomeDirectory = home, Version = 1, Origin = null };
        store.SaveStudent(record, null);
        var program = Path.Combine(root, "publish"); Directory.CreateDirectory(Path.Combine(program, "runtimes")); SchoolDefaultsTests.SelfContainedStub(program);
        File.WriteAllText(Path.Combine(program, "GsmKurswahl.StudentApp.exe"), "exe");
        File.WriteAllText(Path.Combine(program, "runtimes", "lib.dll"), "lib");
        var distribution = new ProfileDistribution(store);
        Assert.Equal("verteilt", distribution.Distribute(record, distribution.Issue(record), Catalog).Status);
        Assert.Equal("verteilt", distribution.DeliverProgram(record, program).Status);
        Assert.Equal("exe", File.ReadAllText(Path.Combine(StudentFolderLayout.Program(home), "GsmKurswahl.StudentApp.exe")));
        Assert.Equal(new DateOnly(2027, 3, 15), JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(StudentFolderLayout.Data(home), "fachangebot.json"))).SubmissionDeadline);
        Assert.Equal(StudentFolderLayout.LauncherContent, File.ReadAllText(StudentFolderLayout.Launcher(home)));
        Assert.Equal([StudentFolderLayout.RootFolder], Directory.EnumerateFileSystemEntries(home).Select(Path.GetFileName));
        // A pupil's own file inside the program folder is never removed.
        File.WriteAllText(Path.Combine(StudentFolderLayout.Program(home), "notiz.txt"), "eigene Datei");

        var choicePath = Path.Combine(StudentFolderLayout.Data(home), "aktuell.json");
        var choiceBytes = JsonDocuments.WriteChoice(Choice with { StudentId = "demo-001" });
        File.WriteAllBytes(choicePath, choiceBytes);
        var notCollected = StudentFolderCleanup.Clean(store, store.Students().Single(), "demo-beratung", new HashSet<string>());
        Assert.Equal("nicht_eingesammelt", notCollected.Status);
        Assert.True(File.Exists(choicePath)); Assert.Empty(store.CollectedFiles("demo-001"));

        var session = new TeacherSession(store, Catalog, "demo-beratung");
        var token = StudentFolderCleanup.Token(choiceBytes);
        session.Confirm(session.Preview(JsonDocuments.ReadChoice(choiceBytes), "demo-001", new(choicePath, token, "m3-choice-v1", T, "")), true, false, "Einsammeln");
        var result = StudentFolderCleanup.Clean(store, store.Students().Single(), "demo-beratung", new HashSet<string>());
        Assert.Equal("teilweise", result.Status);
        Assert.Contains("notiz.txt", result.Detail);
        Assert.False(File.Exists(choicePath)); Assert.False(File.Exists(Path.Combine(StudentFolderLayout.Data(home), "profil.json"))); Assert.False(File.Exists(Path.Combine(StudentFolderLayout.Data(home), "fachangebot.json")));
        Assert.False(File.Exists(Path.Combine(StudentFolderLayout.Program(home), "GsmKurswahl.StudentApp.exe")));
        Assert.False(Directory.Exists(Path.Combine(StudentFolderLayout.Program(home), "runtimes")));
        Assert.True(File.Exists(Path.Combine(StudentFolderLayout.Program(home), "notiz.txt")));
        Assert.False(File.Exists(StudentFolderLayout.Launcher(home)));
        Assert.False(Directory.Exists(StudentFolderLayout.Data(home)));
        var archived = store.CollectedFiles("demo-001");
        Assert.Equal(choiceBytes, archived.Single(f => f.FileName == "aktuell.json").Content);
        Assert.Contains(archived, f => f.FileName == "profil.json");
    }

    [Fact]
    public void FinalCollectionRemovesWholeKurswahlFolderButKeepsHome()
    {
        using var store = new SqliteAdministrationStore(Path.Combine(root, "admin.db"));
        var home = Path.Combine(root, "home"); Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(home, "eigenes.txt"), "Schülerdatei");
        var record = Record with { HomeDirectory = home, Version = 1, Origin = null };
        store.SaveStudent(record, null);
        var program = Path.Combine(root, "publish"); Directory.CreateDirectory(program); SchoolDefaultsTests.SelfContainedStub(program);
        File.WriteAllText(Path.Combine(program, "GsmKurswahl.StudentApp.exe"), "exe");
        var distribution = new ProfileDistribution(store);
        Assert.Equal("verteilt", distribution.Distribute(record, distribution.Issue(record), Catalog).Status);
        Assert.Equal("verteilt", distribution.DeliverProgram(record, program).Status);
        Assert.True(File.Exists(Path.Combine(StudentFolderLayout.Data(home), "profil.json")));

        var result = StudentFolderCleanup.Clean(store, store.Students().Single(), "demo-beratung", new HashSet<string>());
        Assert.Equal("geräumt", result.Status);
        Assert.False(Directory.Exists(StudentFolderLayout.Root(home)));
        Assert.Equal("Schülerdatei", File.ReadAllText(Path.Combine(home, "eigenes.txt")));
    }

    [Fact]
    public void ChangedLauncherIsNeitherReplacedNorRemoved()
    {
        using var store = new SqliteAdministrationStore(Path.Combine(root, "admin.db"));
        var home = Path.Combine(root, "home"); Directory.CreateDirectory(StudentFolderLayout.Root(home));
        File.WriteAllText(StudentFolderLayout.Launcher(home), "fremd");
        var record = Record with { HomeDirectory = home, Version = 1, Origin = null };
        store.SaveStudent(record, null);
        var program = Path.Combine(root, "publish"); Directory.CreateDirectory(program); SchoolDefaultsTests.SelfContainedStub(program);
        File.WriteAllText(Path.Combine(program, "GsmKurswahl.StudentApp.exe"), "exe");
        Assert.Equal("Programm_abweichend", new ProfileDistribution(store).DeliverProgram(record, program).Status);
        Assert.Equal("teilweise", StudentFolderCleanup.Clean(store, store.Students().Single(), "demo-beratung", new HashSet<string>()).Status);
        Assert.Equal("fremd", File.ReadAllText(StudentFolderLayout.Launcher(home)));
    }

    [Fact]
    public void ChangedProgramFileInPupilFolderBlocksRedelivery()
    {
        using var store = new SqliteAdministrationStore(Path.Combine(root, "admin.db"));
        var home = Path.Combine(root, "home"); Directory.CreateDirectory(home);
        var record = Record with { HomeDirectory = home, Version = 1, Origin = null };
        store.SaveStudent(record, null);
        var program = Path.Combine(root, "publish"); Directory.CreateDirectory(program); SchoolDefaultsTests.SelfContainedStub(program);
        File.WriteAllText(Path.Combine(program, "GsmKurswahl.StudentApp.exe"), "v1");
        var distribution = new ProfileDistribution(store);
        Assert.Equal("verteilt", distribution.DeliverProgram(record, program).Status);
        File.WriteAllText(Path.Combine(program, "GsmKurswahl.StudentApp.exe"), "v2");
        Assert.Equal("verteilt", distribution.DeliverProgram(record, program).Status);
        File.WriteAllText(Path.Combine(StudentFolderLayout.Program(home), "GsmKurswahl.StudentApp.exe"), "fremd");
        File.WriteAllText(Path.Combine(program, "GsmKurswahl.StudentApp.exe"), "v3");
        Assert.Equal("Programm_abweichend", distribution.DeliverProgram(record, program).Status);
        Assert.Equal("fremd", File.ReadAllText(Path.Combine(StudentFolderLayout.Program(home), "GsmKurswahl.StudentApp.exe")));
    }
}
