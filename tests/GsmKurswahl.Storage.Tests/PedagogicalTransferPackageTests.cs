using System.IO.Compression;
using System.Text;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Storage.Tests;

public sealed class PedagogicalTransferPackageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "gsm-pkg-test-" + Guid.NewGuid());

    public void Dispose()
    {
        try { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); } catch { }
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private (TeacherWorkspace Workspace, string TeacherDir, string StudentDir) SeedEnvironment(bool selfContainedTeacher = true, bool includeStudent = true)
    {
        var workspace = new TeacherWorkspace(Path.Combine(root, "workspace"));
        workspace.Create(TestData.WorkflowCatalog, "demo-beratung");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Complete });
        using (var store = workspace.OpenActive())
        {
            store.SaveStudent(new(SchemaVersions.StudentRecord,
                new(Guid.NewGuid(), "demo-001", "Demo Paket", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown),
                "", "10a/Paket.Demo", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
        }

        var teacherDir = Path.Combine(root, "TeacherApp");
        Directory.CreateDirectory(teacherDir);
        File.WriteAllText(Path.Combine(teacherDir, "GsmKurswahl.TeacherApp.exe"), "synthetic-exe");
        File.WriteAllText(Path.Combine(teacherDir, "GsmKurswahl.TeacherApp.dll"), "synthetic-dll");
        File.WriteAllText(Path.Combine(teacherDir, TeacherProgramLease.FileName), "held-lock");
        if (selfContainedTeacher)
        {
            File.WriteAllText(Path.Combine(teacherDir, "hostfxr.dll"), "synthetic-hostfxr");
            File.WriteAllText(Path.Combine(teacherDir, "GsmKurswahl.TeacherApp.runtimeconfig.json"),
                """{"runtimeOptions":{"includedFrameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.14"}]}}""");
        }
        else
        {
            File.WriteAllText(Path.Combine(teacherDir, "GsmKurswahl.TeacherApp.runtimeconfig.json"),
                """{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.0.14"}}}""");
        }

        var studentDir = Path.Combine(root, "StudentApp");
        if (includeStudent)
        {
            Directory.CreateDirectory(studentDir);
            File.WriteAllText(Path.Combine(studentDir, "GsmKurswahl.StudentApp.exe"), "synthetic-student-exe");
            File.WriteAllText(Path.Combine(studentDir, "hostfxr.dll"), "synthetic-hostfxr");
            File.WriteAllText(Path.Combine(studentDir, "GsmKurswahl.StudentApp.runtimeconfig.json"),
                """{"runtimeOptions":{"includedFrameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.14"}]}}""");
        }

        return (workspace, teacherDir, studentDir);
    }

    [Fact]
    public void ExportPackage_IncludesProgramsStarterReadmeAndTransferWhileExcludingRuntimeData()
    {
        var (workspace, teacherDir, studentDir) = SeedEnvironment();

        // Noise / runtime files that must never end up in the exported package
        File.WriteAllText(Path.Combine(teacherDir, "verwaltung.db"), "db");
        File.WriteAllText(Path.Combine(teacherDir, "verwaltung.db-wal"), "wal");
        File.WriteAllText(Path.Combine(teacherDir, "verwaltung.db-shm"), "shm");
        File.WriteAllText(Path.Combine(teacherDir, "workspace.json"), "{}");
        File.WriteAllText(Path.Combine(teacherDir, "location.json"), "{}");
        File.WriteAllText(Path.Combine(teacherDir, "old.gsm-transfer"), "transfer");
        File.WriteAllText(Path.Combine(teacherDir, "temp.lock"), "lock");
        File.WriteAllText(Path.Combine(teacherDir, "temp.tmp"), "tmp");
        File.WriteAllText(Path.Combine(teacherDir, "prev.bak"), "bak");
        Directory.CreateDirectory(Path.Combine(teacherDir, "Arbeitsstand"));
        File.WriteAllText(Path.Combine(teacherDir, "Arbeitsstand", "local.db"), "local");
        Directory.CreateDirectory(Path.Combine(teacherDir, "Abitur-2029"));
        File.WriteAllText(Path.Combine(teacherDir, "Abitur-2029", "verwaltung.db"), "db");
        Directory.CreateDirectory(Path.Combine(teacherDir, "Nachwahlboegen"));
        File.WriteAllText(Path.Combine(teacherDir, "Nachwahlboegen", "sheet.pdf"), "pdf");
        Directory.CreateDirectory(Path.Combine(teacherDir, "Netzwechsel"));
        File.WriteAllText(Path.Combine(teacherDir, "Netzwechsel", "note.txt"), "note");
        Directory.CreateDirectory(Path.Combine(teacherDir, ".git"));
        File.WriteAllText(Path.Combine(teacherDir, ".git", "HEAD"), "ref");

        File.WriteAllText(Path.Combine(studentDir, "aktuell.json"), "{}");
        File.WriteAllText(Path.Combine(studentDir, "profil.json"), "{}");
        File.WriteAllText(Path.Combine(studentDir, "choice.json.bak"), "{}");

        var zipPath = Path.Combine(root, "paed-paket.zip");
        var progressCalls = 0;
        PedagogicalPackageResult result;
        using (var lease = new TeacherProgramLease(teacherDir))
        using (var store = workspace.OpenActive())
        {
            result = PedagogicalTransferPackage.Export(
                workspace.Active!, store, zipPath, teacherDir, studentDir, (_, _, _) => progressCalls++);
        }

        Assert.True(progressCalls > 0);
        Assert.Equal(zipPath, result.ArchivePath);
        Assert.True(result.TeacherSelfContained);
        Assert.True(result.StudentIncluded);
        Assert.Equal(5, result.TeacherFileCount);
        Assert.Equal(3, result.StudentFileCount);
        Assert.StartsWith("Transfer/Abitur-2029-", result.TransferEntryName, StringComparison.Ordinal);

        using var archive = ZipFile.OpenRead(zipPath);
        var names = archive.Entries.Select(e => e.FullName).ToArray();
        Assert.Contains(PedagogicalTransferPackage.StarterFileName, names);
        Assert.Contains(PedagogicalTransferPackage.ReadmeFileName, names);
        Assert.Contains(result.TransferEntryName, names);
        Assert.Contains("TeacherApp/GsmKurswahl.TeacherApp.exe", names);
        Assert.Contains("TeacherApp/" + TeacherProgramLease.FileName, names);
        Assert.Contains("StudentApp/GsmKurswahl.StudentApp.exe", names);

        Assert.DoesNotContain(names, n => n.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith("/workspace.json", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith("/location.json", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith("/aktuell.json", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith("/profil.json", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("TeacherApp/Arbeitsstand/", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("TeacherApp/Abitur-", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("TeacherApp/Nachwahlboegen/", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("TeacherApp/Netzwechsel/", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("TeacherApp/.", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("TeacherApp/old.gsm-transfer", StringComparison.OrdinalIgnoreCase));

        using (var starterReader = new StreamReader(archive.GetEntry(PedagogicalTransferPackage.StarterFileName)!.Open(), Encoding.ASCII))
        {
            var starter = starterReader.ReadToEnd();
            Assert.Contains("--paket-ordner \"%~dp0.\"", starter);
        }
        using (var readmeReader = new StreamReader(archive.GetEntry(PedagogicalTransferPackage.ReadmeFileName)!.Open(), Encoding.UTF8))
        {
            var readme = readmeReader.ReadToEnd();
            Assert.Contains("löschen", readme, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(PedagogicalTransferPackage.WorkspaceFolderName, readme);
        }
        using (var leaseReader = new StreamReader(archive.GetEntry("TeacherApp/" + TeacherProgramLease.FileName)!.Open(), Encoding.UTF8))
        {
            Assert.Equal(PedagogicalTransferPackage.LeaseFileContent, leaseReader.ReadToEnd());
        }

        var extracted = Path.Combine(root, "extracted");
        ZipFile.ExtractToDirectory(zipPath, extracted);
        var transferFile = PedagogicalTransferPackage.FindSingleTransferFile(extracted);
        Assert.NotNull(transferFile);
        var manifest = WorkspaceTransfer.Inspect(transferFile!);
        Assert.Equal(2029, manifest.Year.Catalog.GraduationYear);

        var imported = WorkspaceTransfer.Import(transferFile!, Path.Combine(extracted, PedagogicalTransferPackage.WorkspaceFolderName));
        using var importedStore = imported.OpenActive();
        Assert.Single(importedStore.Students());
        Assert.Equal("demo-001", importedStore.Students()[0].Student.ExternalStudentId);

        File.WriteAllText(transferFile + ".second.gsm-transfer", "dummy");
        Assert.Null(PedagogicalTransferPackage.FindSingleTransferFile(extracted));
    }

    [Fact]
    public void ExportPackage_RejectsInvalidDestinationsAndHandlesMissingStudentApp()
    {
        var (workspace, teacherDir, _) = SeedEnvironment(selfContainedTeacher: false, includeStudent: false);
        using var store = workspace.OpenActive();

        Assert.Throws<InvalidOperationException>(() =>
            PedagogicalTransferPackage.Export(workspace.Active!, store, Path.Combine(teacherDir, "inside.zip"), teacherDir));

        var missingExeDir = Path.Combine(root, "empty-teacher");
        Directory.CreateDirectory(missingExeDir);
        Assert.Throws<InvalidOperationException>(() =>
            PedagogicalTransferPackage.Export(workspace.Active!, store, Path.Combine(root, "missing.zip"), missingExeDir));

        var existingZip = Path.Combine(root, "existing.zip");
        File.WriteAllText(existingZip, "keep");
        Assert.Throws<IOException>(() =>
            PedagogicalTransferPackage.Export(workspace.Active!, store, existingZip, teacherDir));

        var partialZip = Path.Combine(root, "teacher-only.zip");
        var result = PedagogicalTransferPackage.Export(workspace.Active!, store, partialZip, teacherDir);
        Assert.False(result.TeacherSelfContained);
        Assert.False(result.StudentIncluded);
        Assert.Equal(0, result.StudentFileCount);

        using var archive = ZipFile.OpenRead(partialZip);
        Assert.DoesNotContain(archive.Entries, e => e.FullName.StartsWith("StudentApp/", StringComparison.Ordinal));
        using var readmeReader = new StreamReader(archive.GetEntry(PedagogicalTransferPackage.ReadmeFileName)!.Open(), Encoding.UTF8);
        var readme = readmeReader.ReadToEnd();
        Assert.Contains("kein selbstenthaltendes Schülerprogramm", readme);
        Assert.Contains(".NET-10-Desktop-Laufzeit", readme);
    }
}
