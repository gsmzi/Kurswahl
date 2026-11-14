using System.Diagnostics;
using GsmKurswahl.AdministrationStorage;

namespace GsmKurswahl.Storage.Tests;

public sealed class TeacherProgramLeaseTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "gsm-startup-lock-" + Guid.NewGuid());
    private string Marker => Path.Combine(root, TeacherProgramLease.FileName);
    public TeacherProgramLeaseTests() => Directory.CreateDirectory(root);
    public void Dispose()
    {
        if (File.Exists(Marker)) File.SetAttributes(Marker, FileAttributes.Normal);
        Directory.Delete(root, true);
    }

    [Fact]
    public void ReadOnlyMarkerBlocksSecondStartAndSurvivesReleaseUnchanged()
    {
        File.WriteAllText(Marker, "GSM demo startup lock");
        File.SetAttributes(Marker, FileAttributes.ReadOnly);
        using (var first = new TeacherProgramLease(root))
            Assert.ThrowsAny<IOException>(() => new TeacherProgramLease(Path.Combine(root, ".")));
        using (var reopened = new TeacherProgramLease(root)) { }
        Assert.Equal("GSM demo startup lock", File.ReadAllText(Marker));
        Assert.True(File.GetAttributes(Marker).HasFlag(FileAttributes.ReadOnly));
    }

    [Fact]
    public void MissingMarkerOrUnavailableDirectoryDoesNotCreateAnything()
    {
        Assert.Throws<FileNotFoundException>(() => new TeacherProgramLease(root));
        Assert.Throws<DirectoryNotFoundException>(() => new TeacherProgramLease(Path.Combine(root, "unavailable")));
        Assert.Empty(Directory.GetFileSystemEntries(root));
    }

    [Fact]
    public void DifferentInstallationsRemainIndependent()
    {
        File.WriteAllText(Marker, "demo");
        var other = Path.Combine(root, "other"); Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, TeacherProgramLease.FileName), "demo");
        using var first = new TeacherProgramLease(root);
        using var second = new TeacherProgramLease(other);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OtherProcessBlocksStartAndReleasesOnExitIncludingForcedTermination(bool terminate)
    {
        if (!OperatingSystem.IsWindows()) return;
        File.WriteAllText(Marker, "demo");
        var info = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.ArgumentList.Add("-NoProfile"); info.ArgumentList.Add("-NonInteractive"); info.ArgumentList.Add("-Command");
        info.ArgumentList.Add("$ErrorActionPreference='Stop'; $lease=[IO.File]::Open($env:GSM_STARTUP_TEST_LOCK,'Open','Read','None'); try { [Console]::Out.WriteLine('ready'); [Console]::In.ReadLine() | Out-Null } finally { $lease.Dispose() }");
        info.Environment["GSM_STARTUP_TEST_LOCK"] = Marker;
        using var process = Process.Start(info)!;
        try
        {
            Assert.Equal("ready", await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20)));
            Assert.Throws<TeacherProgramInUseException>(() => new TeacherProgramLease(root));
            if (terminate) process.Kill(entireProcessTree: true);
            else { await process.StandardInput.WriteLineAsync("close"); await process.StandardInput.FlushAsync(); }
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            // Windows may signal process exit before the final file handle teardown has completed.
            // Await the actual release; never delete or replace a locked marker to make the test pass.
            var deadline = Stopwatch.StartNew();
            while (true)
            {
                try { using var restarted = new TeacherProgramLease(root); break; }
                catch (TeacherProgramInUseException) when (deadline.Elapsed < TimeSpan.FromSeconds(10))
                { await Task.Delay(100); }
            }
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
        Assert.Equal("demo", File.ReadAllText(Marker));
    }
}
