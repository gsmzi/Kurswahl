using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.TeacherApp;

namespace GsmKurswahl.StudentApp.Tests;

public sealed class TeacherProgramStartupTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "gsm-startup-ui-" + Guid.NewGuid());
    public TeacherProgramStartupTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);

    [AvaloniaFact]
    public void BlockedStartShowsMessageAndNeverOpensAdministration()
    {
        File.WriteAllText(Path.Combine(root, TeacherProgramLease.FileName), "demo");
        using var owner = new TeacherProgramLease(root);
        using var startup = new TeacherProgramStartup(root);
        var called = false;
        var window = startup.OpenMainWindow(() => { called = true; return new Window(); });
        try
        {
            window.Show(); window.UpdateLayout();
            Assert.False(called);
            Assert.Contains("bereits verwendet", window.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Name == "StartupLockMessage").Text);
            window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == "Schließen")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(window.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MissingOrInaccessibleMarkerFailsClosedWithoutCreatingData()
    {
        using var startup = new TeacherProgramStartup(root);
        void Check()
        {
            var window = startup.OpenMainWindow(() => throw new InvalidOperationException("Darf keine Verwaltung öffnen."));
            try
            {
                window.Show(); window.UpdateLayout();
                Assert.Contains("Startsperre konnte nicht geöffnet", window.GetLogicalDescendants().OfType<TextBlock>()
                    .Single(t => t.Name == "StartupLockMessage").Text);
            }
            finally { window.Close(); }
        }
        Check(); Assert.Empty(Directory.GetFileSystemEntries(root));
        // A directory instead of the marker is inaccessible as a file, even without writable-file requirements.
        Directory.CreateDirectory(Path.Combine(root, TeacherProgramLease.FileName)); Check();
    }

    [AvaloniaFact]
    public void LocalPackageCopiesAndTestStagesUseTheSharedProgramLockBeforePreparation()
    {
        var shared = Path.Combine(root, "shared [Demo]"); var local = Path.Combine(root, "local");
        Directory.CreateDirectory(shared); Directory.CreateDirectory(local);
        File.WriteAllText(Path.Combine(shared, TeacherProgramLease.FileName), "demo");
        File.WriteAllText(Path.Combine(local, TeacherProgramLease.FileName), "demo");
        using var owner = new TeacherProgramLease(shared);
        using var startup = TeacherProgramStartup.ForArguments(
            ["--test-stage", "lehrer-1-anfang", "demo-root", "--startsperre", shared], local, out var args);
        Assert.Equal(["--test-stage", "lehrer-1-anfang", "demo-root"], args);
        var window = startup.OpenMainWindow(() => throw new InvalidOperationException("Teststand darf nicht vorbereitet werden."));
        try
        {
            window.Show(); window.UpdateLayout();
            Assert.Contains("bereits verwendet", window.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Name == "StartupLockMessage").Text);
            using var localStillFree = new TeacherProgramLease(local);
        }
        finally { window.Close(); }
        owner.Dispose(); File.Delete(Path.Combine(shared, TeacherProgramLease.FileName));
        using var missingShared = TeacherProgramStartup.ForArguments(["--startsperre", shared], local, out _);
        var error = missingShared.OpenMainWindow(() => throw new InvalidOperationException("Kein lokaler Ersatz zulässig."));
        try
        {
            error.Show(); error.UpdateLayout();
            Assert.Contains("Startsperre konnte nicht geöffnet", error.GetLogicalDescendants().OfType<TextBlock>()
                .Single(t => t.Name == "StartupLockMessage").Text);
            using var localStillFree = new TeacherProgramLease(local);
        }
        finally { error.Close(); }
    }

    [AvaloniaFact]
    public void SuccessfulStartHoldsLockForLifetimeAndConstructorFailureReleasesIt()
    {
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, TeacherProgramLease.FileName)));
        File.WriteAllText(Path.Combine(root, TeacherProgramLease.FileName), "demo");
        using (var startup = TeacherProgramStartup.ForArguments([], root, out var args))
        {
            Assert.Empty(args);
            var window = startup.OpenMainWindow(() => new Window());
            try { Assert.ThrowsAny<IOException>(() => new TeacherProgramLease(root)); }
            finally { window.Close(); }
        }
        using (var startup = new TeacherProgramStartup(root))
            Assert.Throws<InvalidOperationException>(() => startup.OpenMainWindow(() => throw new InvalidOperationException("demo")));
        using var restarted = new TeacherProgramLease(root);
    }
}
