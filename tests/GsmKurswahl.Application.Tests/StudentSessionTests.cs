using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Application.Tests;

public class StudentSessionTests
{
    [Fact] public async Task SelectSaveLoadPreservesDraftAndDoesNotInflateRevisionOnUnchangedSave()
    {
        var store = new MemoryStore();
        var session = new StudentSession(TestData.Catalog, "demo-001", store);
        session.SetSelection("D-BF", [1, 2, 3, 4]);
        await session.SaveAsync(); await session.SaveAsync();
        Assert.Equal(1, store.Writes);
        var second = new StudentSession(TestData.Catalog, "demo-001", store);
        await second.LoadAsync();
        Assert.Single(second.Choice.Selections);
        Assert.Equal(ReviewStatus.NeedsReview, second.Validation.Status);
        Assert.Equal(SubmissionStatus.Draft, second.Choice.SubmissionStatus);
    }
    [Fact] public void DuplicateSubjectIsNotSilentlyReplaced()
    {
        var catalog = TestData.Catalog with { Offers = [TestData.Offer(), TestData.Offer("D-LF", type: CourseType.Lf)] };
        var session = new StudentSession(catalog, "demo-001", new MemoryStore());
        session.SetSelection("D-BF", [1, 2]);
        Assert.Throws<InvalidOperationException>(() => session.SetSelection("D-LF", [1, 2, 3, 4]));
        Assert.Equal("D-BF", Assert.Single(session.Choice.Selections).OfferId);
    }
    [Fact] public async Task FailedSaveRetainsDirtyChoiceAndRevision()
    {
        var session = new StudentSession(TestData.Catalog, "demo-001", new MemoryStore { FailSave = true });
        session.SetSelection("D-BF", [1, 2]);
        await Assert.ThrowsAsync<IOException>(() => session.SaveAsync());
        Assert.True(session.IsDirty); Assert.Single(session.Choice.Selections); Assert.Equal(1, session.Choice.Revision);
    }
    [Fact] public async Task FailedLoadPreservesCurrentChoiceAndBlocksSaving()
    {
        var store = new MemoryStore { Value = new(TestData.Choice with { StudentId = "demo-other" }, "token") };
        var session = new StudentSession(TestData.Catalog, "demo-001", store);
        session.SetSelection("D-BF", [1, 2]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.LoadAsync(discardChanges: true));
        Assert.Single(session.Choice.Selections); Assert.True(session.LoadFailed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SaveAsync());
        Assert.Equal(0, store.Writes);
    }
    [Fact] public async Task DirtyLoadNeedsExplicitDiscard()
    {
        var session = new StudentSession(TestData.Catalog, "demo-001", new MemoryStore());
        session.SetSelection("D-BF", [1, 2]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.LoadAsync());
        Assert.True(session.IsDirty);
    }
    [Fact] public void MissingProfileCannotEnableBilingualOffer()
    {
        var offer = TestData.Offer() with { Variant = CourseVariant.Bilingual, Requirements = [Requirement.ContinuousBilingual] };
        var session = new StudentSession(TestData.Catalog with { Offers = [offer] }, "demo-001", new MemoryStore());
        Assert.Throws<InvalidOperationException>(() => session.SetSelection(offer.Id, [1, 2, 3, 4]));
    }
    [Fact] public void ProductionCatalogRejected() => Assert.Throws<ArgumentException>(() =>
        new StudentSession(TestData.Catalog with { IsDemo = false }, "demo-001", new MemoryStore()));

    private sealed class MemoryStore : IChoiceStore
    {
        public StoredChoice? Value { get; set; }
        public bool FailSave { get; init; }
        public int Writes { get; private set; }
        public Task<StoredChoice?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public Task<StoredChoice> SaveAsync(CourseChoice choice, string? expectedToken, CancellationToken cancellationToken = default)
        {
            if (FailSave) throw new IOException("synthetic failure");
            Writes++; Value = new(choice, Writes.ToString()); return Task.FromResult(Value);
        }
    }
}
