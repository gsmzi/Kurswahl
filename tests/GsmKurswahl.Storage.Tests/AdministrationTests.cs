using System.Security.Cryptography;
using GsmKurswahl.Application;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;
using Microsoft.Data.Sqlite;

namespace GsmKurswahl.Storage.Tests;

public sealed class AdministrationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "gsm-admin-" + Guid.NewGuid());
    public AdministrationTests() => Directory.CreateDirectory(root);
    private string DbPath => Path.Combine(root, "admin.db");
    private StudentRecord Record(int number = 1) => new(1,
        new(Guid.NewGuid(), $"demo-{number:000}", $"Demo Person {number}", "10-demo", 2029, DepartureStatus.Active, PriorEducation.Unknown),
        $"DEMO\\test{number}", $"S-1-5-21-100-200-300-{number}", Path.Combine(root, "home" + number), true, 1, "demo-beratung", DateTimeOffset.UtcNow, null);
    [Fact]
    public void RelativeReimportKeepsResolvedMappingAndOnlyReportsChangedPrerequisites()
    {
        using var store = new SqliteAdministrationStore(DbPath);
        var current = Record() with { HomeDirectory = Path.Combine(root, "10a", "Muster.Demo"), Origin = new("demo.csv", "old", "map", DateTimeOffset.UtcNow, "demo-beratung") };
        store.SaveStudent(current, null);
        var incoming = current with { HomeDirectory = "10a/Muster.Demo", MappingConfirmed = false, Origin = current.Origin! with { ContentHash = "new" }, Student = current.Student with { Prerequisites = PriorEducation.Unknown with { InformaticsImp = Knowledge.Yes } } };
        Assert.Equal("Voraussetzungen", StudentImportService.Differences(current, incoming));
        Assert.True(StudentImportService.Confirm(store, incoming, 1, true));
        Assert.True(store.Students().Single().MappingConfirmed);
        Assert.Equal(current.HomeDirectory, store.Students().Single().HomeDirectory);
    }
    [Fact]
    public void DistributionRootResolvesExistingClassFoldersAndRejectsEscapeOrMissingTargets()
    {
        var target = Path.Combine(root, "10a", "Muster.Demo"); Directory.CreateDirectory(target);
        Assert.Equal(target, StudentDirectories.Resolve(root, "10a/Muster.Demo"));
        Assert.Equal(target, StudentDirectories.Resolve(root, target));
        Assert.Throws<ArgumentException>(() => StudentDirectories.Resolve(root, "../outside"));
        Assert.Throws<DirectoryNotFoundException>(() => StudentDirectories.Resolve(root, "10b/Missing.Demo"));
        Assert.False(Directory.Exists(Path.Combine(root, "10b")));
    }
    [Fact]
    public void UnchangedRowsInChangedImportFileKeepConfirmedMappingAndRevision()
    {
        using var store = new SqliteAdministrationStore(DbPath);
        var current = Record() with { Origin = new("demo.csv", "old", "map", DateTimeOffset.UtcNow, "demo-beratung") };
        store.SaveStudent(current, null);
        var incoming = current with { Student = current.Student with { Id = Guid.NewGuid() }, MappingConfirmed = false, Origin = current.Origin! with { ContentHash = "new" } };
        Assert.False(StudentImportService.Confirm(store, incoming, 1, true));
        Assert.True(store.Students().Single().MappingConfirmed); Assert.Equal(1, store.Students().Single().Version);
    }
    [Fact]
    public void DashboardUsesCurrentPrerequisitesAndLatestDeliveryInsteadOfCountingOldSuccess()
    {
        using var store = new SqliteAdministrationStore(DbPath); var record = Record(); store.SaveStudent(record, null);
        var session = new TeacherSession(store, TestData.Catalog, "demo-beratung");
        Assert.Equal(1, TeacherDashboardBuilder.Build(store, session).ProfilesToIssue);
        Directory.CreateDirectory(record.HomeDirectory);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var service = new ProfileDistribution(store);
        var profile = service.Issue(record, key, "demo-key");
        Assert.Equal(1, TeacherDashboardBuilder.Build(store, session).ProfilesToDeliver);
        Assert.Equal("verteilt", service.Distribute(record, profile).Status);
        Assert.Equal(1, TeacherDashboardBuilder.Build(store, session).Delivered);
        store.LogDistribution(new(Guid.NewGuid(), record.Student.ExternalStudentId, profile.Profile.ProfileRevision, DateTimeOffset.UtcNow, "Zugriff_verweigert", "Synthetischer Zustellfehler"));
        Assert.Equal(0, TeacherDashboardBuilder.Build(store, session).Delivered);
        Assert.Equal(1, TeacherDashboardBuilder.Build(store, session).ProfilesToDeliver);
        var corrected = record with { Version = 2, ChangedAt = DateTimeOffset.UtcNow, Student = record.Student with { Prerequisites = PriorEducation.Unknown with { InformaticsImp = Knowledge.Yes } } };
        store.SaveStudent(corrected, 1);
        Assert.Equal(1, TeacherDashboardBuilder.Build(store, session).ProfilesToIssue);
        Assert.Equal(0, TeacherDashboardBuilder.Build(store, session).Delivered);
    }
    [Fact]
    public void ReimportPreservesAdministrativeCorrectionsAndChoiceHistory()
    {
        using var store = new SqliteAdministrationStore(DbPath);
        var imported = Record() with { Origin = new("demo.csv", "hash1", "mapping1", DateTimeOffset.UtcNow, "demo-beratung") };
        Assert.Throws<InvalidOperationException>(() => StudentImportService.Confirm(store, imported, null, false));
        Assert.True(StudentImportService.Confirm(store, imported, null, true));
        var current = store.Students().Single();
        store.SaveStudent(current with { Version = 2, Student = current.Student with { Prerequisites = PriorEducation.Unknown with { InformaticsImp = Knowledge.Yes } } }, 1);
        var session = new TeacherSession(store, TestData.Catalog, "demo-beratung");
        session.Save(TestData.Choice, null, RevisionSource.TeacherEdit, "Lehrerwahl");
        Assert.False(StudentImportService.Confirm(store, imported, 2, true));
        Assert.Equal(Knowledge.Yes, store.Students().Single().Student.Prerequisites.InformaticsImp);
        Assert.Single(store.History("demo-001"));
        Assert.True(StudentImportService.Confirm(store, imported with { Origin = imported.Origin! with { ContentHash = "hash2" } }, 2, true));
        Assert.Equal(3, store.Students().Single().Version); Assert.Single(store.History("demo-001"));
        Assert.False(store.Students().Single().MappingConfirmed);
    }
    [Fact]
    public void SingleWriterUnknownSchemaAndCorruptDatabaseAreRejected()
    {
        using (var store = new SqliteAdministrationStore(DbPath))
            Assert.Throws<IOException>(() => new SqliteAdministrationStore(DbPath));
        using (var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False"))
        { connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "PRAGMA user_version=99"; command.ExecuteNonQuery(); }
        Assert.Throws<InvalidDataException>(() => new SqliteAdministrationStore(DbPath));
        File.WriteAllText(Path.Combine(root, "broken.db"), "synthetic broken database");
        Assert.Throws<SqliteException>(() => new SqliteAdministrationStore(Path.Combine(root, "broken.db")));
    }
    [Fact]
    public void CollectionPreservesTeacherChangesAndRestorationAppends()
    {
        using var store = new SqliteAdministrationStore(DbPath); var record = Record(); store.SaveStudent(record, null);
        var session = new TeacherSession(store, TestData.Catalog, "demo-beratung");
        var origin = new ImportOrigin("demo-home/aktuell.json", "hash1", "v1", DateTimeOffset.UtcNow, record.WindowsUsername);
        var initial = session.Preview(TestData.Choice, record.Student.ExternalStudentId, origin);
        Assert.Throws<InvalidOperationException>(() => session.Confirm(initial, false, false, "Test"));
        session.Confirm(initial, true, false, "Erste Übernahme");
        var first = store.History("demo-001").Single();
        var edited = session.Save(first.Snapshot with { Selections = [TestData.Select(TestData.Offer())] }, first.Id, RevisionSource.TeacherEdit, "Synthetische Korrektur");
        var repeat = session.Preview(first.Snapshot, "demo-001", origin);
        Assert.True(repeat.Duplicate); session.Confirm(repeat, true, false, "Wiederholung"); Assert.Equal(2, store.History("demo-001").Length);
        var stale = session.Preview(first.Snapshot, "demo-001", origin with { ContentHash = "changed" });
        Assert.NotEmpty(stale.Conflict); Assert.Throws<InvalidOperationException>(() => session.Confirm(stale, true, false, "Altdatei"));
        var restored = session.Restore("demo-001", first.Id, edited.Id, "Bewusste Wiederherstellung");
        Assert.Equal(edited.Id, restored.PreviousRevisionId); Assert.Equal(3, store.History("demo-001").Length);
        Assert.Empty(restored.Snapshot.Selections); Assert.Single(restored.Changes);
        Assert.Throws<InvalidOperationException>(() => session.Save(first.Snapshot, first.Id, RevisionSource.TeacherEdit, "Veralteter Stand"));
        Assert.Throws<InvalidOperationException>(() => session.FollowUp("demo-001", false, null, "Nicht freigeben"));
    }
    [Fact]
    public void PrerequisiteCorrectionInvalidatesPreviewAndUsesProtectedData()
    {
        using var store = new SqliteAdministrationStore(DbPath); var record = Record(); store.SaveStudent(record, null);
        var offer = TestData.Offer("INF-BF", "INF") with { Requirements = [Requirement.Informatics] };
        var catalog = TestData.Catalog with { Subjects = [new("INF", "Informatik")], Offers = [offer] };
        var choice = TestData.Choice with { Selections = [TestData.Select(offer)] };
        var session = new TeacherSession(store, catalog, "demo-beratung");
        var origin = new ImportOrigin("demo-home", "hash", "v1", DateTimeOffset.UtcNow, record.WindowsUsername);
        var preview = session.Preview(choice, "demo-001", origin);
        var corrected = record with { Version = 2, Student = record.Student with { Prerequisites = PriorEducation.Unknown with { InformaticsBridge = Knowledge.No, InformaticsImp = Knowledge.No } } };
        store.SaveStudent(corrected, 1);
        Assert.Throws<InvalidOperationException>(() => session.Confirm(preview, true, false, "Veraltet"));
        var updated = session.Preview(choice, "demo-001", origin);
        Assert.Contains(updated.Validation.Messages, m => m.Rule.Id == "PREREQUISITE-NO");
        session.Confirm(updated, true, false, "Mit Prüffehler übernommen");
        Assert.True(session.Overview().Single().RequiresReselection);
        Assert.Throws<InvalidOperationException>(() => store.SaveStudent(corrected, 1));
        Assert.Throws<InvalidOperationException>(() => store.SaveStudent(Record(2) with { AccountId = record.AccountId }, null));
    }
    [Fact]
    public void SignaturesRoundTripAndOldProfileCannotWinAgainstCorrection()
    {
        using var store = new SqliteAdministrationStore(DbPath); var record = Record(); store.SaveStudent(record, null); Directory.CreateDirectory(record.HomeDirectory);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var distribution = new ProfileDistribution(store);
        var issued = distribution.Issue(record, key, "demo-key");
        var reader = new SignedProfileReader(new(1, [new("demo-key", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()))], ["DEMO"]), new Identity(record));
        Assert.Equal("verteilt", distribution.Distribute(record, issued).Status);
        Assert.Equal(ProfileStatus.Verified, reader.Read(record.HomeDirectory, "demo-001", 2029).Status);
        Assert.Equal("verteilt", distribution.Distribute(record, issued).Status); Assert.Empty(store.History("demo-001"));
        var corrected = record with { Version = 2, Student = record.Student with { Prerequisites = PriorEducation.Unknown with { InformaticsImp = Knowledge.Yes } } }; store.SaveStudent(corrected, 1);
        Assert.Equal("veraltet", distribution.Distribute(corrected, issued).Status);
        var newer = distribution.Issue(corrected, key, "demo-key"); Assert.Equal(2, newer.Profile.ProfileRevision);
        Assert.Equal("verteilt", distribution.Distribute(corrected, newer).Status);
        Assert.Equal(Knowledge.Yes, reader.Read(record.HomeDirectory, "demo-001", 2029).Prerequisites.InformaticsImp);
        Assert.Equal("veraltet", distribution.Distribute(record, issued).Status);
    }
    [Fact]
    public void HundredProfilesContinueAfterOneLockedTargetWithoutChangingChoices()
    {
        using var store = new SqliteAdministrationStore(DbPath); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var distribution = new ProfileDistribution(store); var records = Enumerable.Range(1, 100).Select(Record).ToArray();
        foreach (var record in records) { store.SaveStudent(record, null); Directory.CreateDirectory(record.HomeDirectory); File.WriteAllText(Path.Combine(record.HomeDirectory, "aktuell.json"), "synthetic unchanged choice"); }
        using var locked = new FileStream(Path.Combine(records[49].HomeDirectory, ".profil.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var reports = records.Select(r => distribution.Distribute(r, distribution.Issue(r, key, "demo-key"))).ToArray();
        Assert.Equal(99, reports.Count(r => r.Status == "verteilt")); Assert.Single(reports, r => r.Status == "Zugriff_verweigert");
        Assert.Equal(100, store.DistributionLog().Length);
        Assert.All(records, r => Assert.Equal("synthetic unchanged choice", File.ReadAllText(Path.Combine(r.HomeDirectory, "aktuell.json"))));
    }
    [Fact]
    public void CorruptOrForeignProfileIsNotOverwritten()
    {
        using var store = new SqliteAdministrationStore(DbPath); var record = Record(); store.SaveStudent(record, null); Directory.CreateDirectory(record.HomeDirectory);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var distribution = new ProfileDistribution(store); var issued = distribution.Issue(record, key, "demo-key");
        var path = Path.Combine(record.HomeDirectory, "profil.json"); File.WriteAllText(path, "broken");
        Assert.Equal("Profil_abweichend", distribution.Distribute(record, issued).Status); Assert.Equal("broken", File.ReadAllText(path));
    }
    private sealed class Identity(StudentRecord record) : ICurrentIdentityProvider { public CurrentIdentity GetCurrent() => new(record.WindowsUsername, record.AccountId); }
    public void Dispose() => Directory.Delete(root, true);
}
