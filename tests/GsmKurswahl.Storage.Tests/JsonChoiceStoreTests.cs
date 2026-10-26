using System.Text;
using System.Text.Json;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Storage.Tests;

public sealed class JsonChoiceStoreTests : IDisposable
{
    [Fact] public async Task LegacySchemaIsRejectedAndFileRemainsUntouched()
    {
        Directory.CreateDirectory(directory);
        var legacy = Encoding.UTF8.GetString(JsonDocuments.WriteChoice(TestData.Choice))
            .Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1");
        await File.WriteAllTextAsync(FilePath, legacy);
        var store = new JsonChoiceStore(FilePath);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => store.LoadAsync());
        Assert.Contains("Version 2 erforderlich", error.Message);
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(TestData.Choice, null));
        Assert.Equal(legacy, await File.ReadAllTextAsync(FilePath));
    }

    [Fact] public void MissingOralPlanningFieldIsRejected()
    {
        var json = Encoding.UTF8.GetString(JsonDocuments.WriteChoice(TestData.Choice));
        json = json.Replace(",\r\n  \"plannedOralSubjects\": []", "").Replace(",\n  \"plannedOralSubjects\": []", "");
        Assert.Throws<JsonException>(() => JsonDocuments.ReadChoice(Encoding.UTF8.GetBytes(json)));
    }
    private readonly string directory = Path.Combine(Path.GetTempPath(), "gsm-tests-" + Guid.NewGuid());
    private string FilePath => Path.Combine(directory, "aktuell.json");
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [Fact] public async Task RoundTripPreservesVersionAndSelection()
    {
        var choice = TestData.Choice with { Selections = [TestData.Select(TestData.Offer())] };
        var saved = await new JsonChoiceStore(FilePath).SaveAsync(choice, null);
        var loaded = await new JsonChoiceStore(FilePath).LoadAsync();
        Assert.Equal(saved.ContentToken, loaded!.ContentToken);
        Assert.Equal(JsonDocuments.WriteChoice(choice), JsonDocuments.WriteChoice(loaded.Choice));
    }
    [Fact] public async Task MissingFileReturnsNull() => Assert.Null(await new JsonChoiceStore(FilePath).LoadAsync());
    [Fact] public async Task ReplacementKeepsPreviousFileAsBackup()
    {
        var store = new JsonChoiceStore(FilePath);
        var first = await store.SaveAsync(TestData.Choice, null);
        await store.SaveAsync(first.Choice with { Revision = 2 }, first.ContentToken);
        Assert.Equal(1, JsonDocuments.ReadChoice(await File.ReadAllBytesAsync(FilePath + ".bak")).Revision);
        Assert.Equal(2, (await store.LoadAsync())!.Choice.Revision);
    }
    [Fact] public async Task InterruptedCommitLeavesOriginalIntact()
    {
        var first = await new JsonChoiceStore(FilePath).SaveAsync(TestData.Choice, null);
        var store = new JsonChoiceStore(FilePath, committer: new FailingCommitter());
        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(first.Choice with { Revision = 2 }, first.ContentToken));
        Assert.Equal(first.ContentToken, (await store.LoadAsync())!.ContentToken);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }
    [Fact] public async Task CancelledWriteDoesNotDestroyOriginal()
    {
        var store = new JsonChoiceStore(FilePath);
        var first = await store.SaveAsync(TestData.Choice, null);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(first.Choice with { Revision = 2 }, first.ContentToken, new CancellationToken(true)));
        Assert.Equal(first.ContentToken, (await store.LoadAsync())!.ContentToken);
    }
    [Fact] public async Task StaleWriterCannotOverwriteNewerChoice()
    {
        var store = new JsonChoiceStore(FilePath);
        var first = await store.SaveAsync(TestData.Choice, null);
        var second = await store.SaveAsync(first.Choice with { Revision = 2 }, first.ContentToken);
        await Assert.ThrowsAsync<ChoiceConflictException>(() => store.SaveAsync(first.Choice with { Revision = 2 }, first.ContentToken));
        Assert.Equal(second.ContentToken, (await store.LoadAsync())!.ContentToken);
    }
    [Fact] public async Task ExclusiveLockPreventsWrite()
    {
        Directory.CreateDirectory(directory);
        using var held = new FileStream(FilePath + ".lock", FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        await Assert.ThrowsAsync<IOException>(() => new JsonChoiceStore(FilePath).SaveAsync(TestData.Choice, null));
        Assert.False(File.Exists(FilePath));
    }
    [Fact] public async Task CorruptFileCannotBeOverwrittenAsNewDraft()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(FilePath, "{broken");
        var store = new JsonChoiceStore(FilePath);
        await Assert.ThrowsAnyAsync<JsonException>(() => store.LoadAsync());
        await Assert.ThrowsAnyAsync<JsonException>(() => store.SaveAsync(TestData.Choice, null));
        Assert.Equal("{broken", await File.ReadAllTextAsync(FilePath));
    }
    [Fact] public void FutureSchemaRejected() => Assert.Throws<ArgumentException>(() => JsonDocuments.ReadChoice(
        Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(JsonDocuments.WriteChoice(TestData.Choice)).Replace("\"schemaVersion\": 2", "\"schemaVersion\": 99"))));
    [Theory] [InlineData("{}")] [InlineData("null")] [InlineData("{\"schemaVersion\":2}")]
    public void MissingFieldsRejected(string json) => Assert.Throws<JsonException>(() => JsonDocuments.ReadChoice(Encoding.UTF8.GetBytes(json)));
    [Fact] public void UnknownFieldsRejected() => Assert.Throws<JsonException>(() => JsonDocuments.ReadChoice(
        Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(JsonDocuments.WriteChoice(TestData.Choice)).Replace("\"schemaVersion\": 2", "\"schemaVersion\": 2, \"valid\": true"))));
    [Fact] public async Task DifferentIdentityCannotReplaceExistingDocument()
    {
        var store = new JsonChoiceStore(FilePath);
        var first = await store.SaveAsync(TestData.Choice, null);
        await Assert.ThrowsAsync<ChoiceConflictException>(() => store.SaveAsync(first.Choice with { Revision = 2, StudentId = "demo-other" }, first.ContentToken));
    }
    [Theory] [InlineData("../escape", "aktuell.json")] [InlineData("Kurswahl", "../x.json")]
    [InlineData("Kurswahl", "profil.json")] [InlineData("Kurswahl", "x.txt")]
    public void UnsafeRelativePathsRejected(string folder, string name) => Assert.Throws<ArgumentException>(() =>
        new StudentSettings(1, directory, folder, name, "catalog.json", "demo-001").ResolveChoicePath());
    [Fact] public void ConfigurablePathUsesSpecifiedHome()
    {
        var settings = new StudentSettings(1, directory, "Demo", "wahl.json", "catalog.json", "demo-001");
        Assert.Equal(Path.Combine(directory, "Demo", "wahl.json"), settings.ResolveChoicePath());
        Assert.Equal(Path.GetFullPath(directory), settings.ResolveHomeDirectory());
    }

    [Fact] public async Task MissingStorageRootIsReportedNotTreatedAsEmpty()
    {
        // Simulates a disconnected network home: the configured root itself is missing.
        var store = new JsonChoiceStore(Path.Combine(directory, "Kurswahl", "aktuell.json"), directory);
        await Assert.ThrowsAsync<StorageUnavailableException>(() => store.LoadAsync());
    }
    [Fact] public async Task SaveNeverCreatesMissingStorageRoot()
    {
        var store = new JsonChoiceStore(Path.Combine(directory, "Kurswahl", "aktuell.json"), directory);
        await Assert.ThrowsAsync<StorageUnavailableException>(() => store.SaveAsync(TestData.Choice, null));
        Assert.False(Directory.Exists(directory));
    }
    [Fact] public async Task MissingSubfolderUnderExistingRootIsNewDraft()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "Kurswahl", "aktuell.json");
        var store = new JsonChoiceStore(path, directory);
        Assert.Null(await store.LoadAsync());
        await store.SaveAsync(TestData.Choice, null);
        Assert.True(File.Exists(path));
    }
    [Fact] public void TargetOutsideStorageRootRejected() => Assert.Throws<ArgumentException>(() =>
        new JsonChoiceStore(Path.Combine(Path.GetTempPath(), "anderswo.json"), directory));

    private sealed class FailingCommitter : IFileCommitter
    {
        public void Commit(string temporaryPath, string targetPath, bool exists) => throw new IOException("Simulierter Abbruch vor Dateiaustausch.");
    }
}
