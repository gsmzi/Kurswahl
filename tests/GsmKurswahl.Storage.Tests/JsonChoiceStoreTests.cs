using System.Text;
using System.Text.Json;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Storage.Tests;

public sealed class JsonChoiceStoreTests : IDisposable
{
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
        var store = new JsonChoiceStore(FilePath, new FailingCommitter());
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
        await Assert.ThrowsAsync<JsonException>(() => store.LoadAsync());
        await Assert.ThrowsAsync<JsonException>(() => store.SaveAsync(TestData.Choice, null));
        Assert.Equal("{broken", await File.ReadAllTextAsync(FilePath));
    }
    [Fact] public void FutureSchemaRejected() => Assert.Throws<ArgumentException>(() => JsonDocuments.ReadChoice(
        Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(JsonDocuments.WriteChoice(TestData.Choice)).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"))));
    [Theory] [InlineData("{}")] [InlineData("null")] [InlineData("{\"schemaVersion\":1}")]
    public void MissingFieldsRejected(string json) => Assert.Throws<JsonException>(() => JsonDocuments.ReadChoice(Encoding.UTF8.GetBytes(json)));
    [Fact] public void UnknownFieldsRejected() => Assert.Throws<JsonException>(() => JsonDocuments.ReadChoice(
        Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(JsonDocuments.WriteChoice(TestData.Choice)).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"valid\": true"))));
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
    [Fact] public void ConfigurablePathUsesSpecifiedHome() => Assert.Equal(Path.Combine(directory, "Demo", "wahl.json"),
        new StudentSettings(1, directory, "Demo", "wahl.json", "catalog.json", "demo-001").ResolveChoicePath());

    private sealed class FailingCommitter : IFileCommitter
    {
        public void Commit(string temporaryPath, string targetPath, bool exists) => throw new IOException("Simulierter Abbruch vor Dateiaustausch.");
    }
}
