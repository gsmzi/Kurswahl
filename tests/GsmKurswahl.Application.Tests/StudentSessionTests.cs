using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Application.Tests;

public class StudentSessionTests
{
    [Fact] public async Task NewProfileReevaluatesExistingChoiceWithoutChangingDraft()
    {
        var inf = TestData.Offer("INF-BF", "INF") with { Requirements = [Requirement.Informatics] };
        var catalog = TestData.Catalog with { Subjects = TestData.Catalog.Subjects.Add(new("INF", "Informatik")), Offers = TestData.Catalog.Offers.Add(inf) };
        var store = new MemoryStore();
        var old = new StudentSession(catalog, "demo-001", store, new(ProfileStatus.Verified, PriorEducation.Unknown with { InformaticsImp = Knowledge.Yes }));
        old.SetSelection("INF-BF", [1, 2, 3, 4]);
        await old.SaveAsync();
        var updated = new StudentSession(catalog, "demo-001", store, new(ProfileStatus.Verified, PriorEducation.Unknown with { InformaticsImp = Knowledge.No, InformaticsBridge = Knowledge.No }) { Revision = 4 });
        await updated.LoadAsync();
        Assert.Equal(old.Choice, updated.Choice);
        Assert.Contains(updated.Validation.Messages, m => m.Rule.Id == "PREREQUISITE-NO");
        Assert.Contains("Revision 4", updated.ProfileStatusText);
        Assert.Equal(1, store.Writes);
        var invalid = new StudentSession(catalog, "demo-001", store, new(ProfileStatus.ProfileInvalid, PriorEducation.Unknown));
        await invalid.LoadAsync();
        Assert.Equal(old.Choice, invalid.Choice);
        Assert.Contains(invalid.Validation.Messages, m => m.Rule.Id == "PROFILE-TRUST");
    }
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
        Assert.Equal(ReviewStatus.Invalid, second.Validation.Status);
        Assert.Equal(SubmissionStatus.Draft, second.Choice.SubmissionStatus);
    }
    [Fact] public void PromotingBasicToLfReplacesItWithVisibleNoticeAndRejectsReverseDuplicate()
    {
        var catalog = TestData.Catalog with { Offers = [TestData.Offer(), TestData.Offer("D-LF", type: CourseType.Lf)] };
        var session = new StudentSession(catalog, "demo-001", new MemoryStore());
        session.SetSelection("D-BF", [1, 2]);
        session.SetSelection("D-LF", [1, 2, 3, 4]);
        Assert.Equal("D-LF", Assert.Single(session.Choice.Selections).OfferId);
        Assert.Contains("Basisfachbelegung wurde automatisch entfernt", session.LfChangeMessage);
        Assert.True(session.IsDirty);
        var choice = session.Choice;
        Assert.Throws<InvalidOperationException>(() => session.SetSelection("D-BF", [1, 2, 3, 4]));
        Assert.Equal(choice, session.Choice);
    }

    [Fact] public void UnavailableLfDoesNotRemoveBasicSelection()
    {
        var lf = TestData.Offer("D-LF", type: CourseType.Lf) with { Requirements = [Requirement.ContinuousBilingual] };
        var session = new StudentSession(TestData.Catalog with { Offers = [TestData.Offer(), lf] }, "demo-001", new MemoryStore());
        session.SetSelection("D-BF", [1, 2, 3, 4]);
        var before = session.Choice;
        Assert.Throws<InvalidOperationException>(() => session.SetSelection(lf.Id, [1, 2, 3, 4]));
        Assert.Equal(before, session.Choice);
        Assert.Empty(session.LfChangeMessage);
    }

    [Fact] public void PromotingBilingualBasicToNormalLfReplacesSubjectAcrossVariants()
    {
        var basic = TestData.Offer("BIO-BF-BILI", "BIO") with { Variant = CourseVariant.Bilingual, Requirements = [Requirement.ContinuousBilingual] };
        var lf = TestData.Offer("BIO-LF", "BIO", CourseType.Lf);
        var catalog = TestData.Catalog with { Subjects = [new("BIO", "Biologie")], Offers = [basic, lf] };
        var session = new StudentSession(catalog, "demo-001", new MemoryStore(),
            new(ProfileStatus.Verified, PriorEducation.Unknown with { ContinuousBilingual = Knowledge.Yes }));
        session.SetSelection(basic.Id, [1, 2, 3, 4]);
        session.SetSelection(lf.Id, [1, 2, 3, 4]);
        Assert.Equal(lf.Id, Assert.Single(session.Choice.Selections).OfferId);
        Assert.Contains("Biologie", session.LfChangeMessage);
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
    [Fact] public async Task VanishedFileKeepsDraftAndNextSaveOnlyCreates()
    {
        var store = new MemoryStore();
        var session = new StudentSession(TestData.Catalog, "demo-001", store);
        session.SetSelection("D-BF", [1, 2]);
        await session.SaveAsync();
        store.Value = null; // file deleted or moved outside the application
        await session.LoadAsync();
        Assert.False(session.LoadFailed); Assert.True(session.IsDirty);
        Assert.Single(session.Choice.Selections);
        await session.SaveAsync();
        Assert.Equal(2, store.Writes); Assert.Null(store.LastExpectedToken);
    }
    [Fact] public async Task DiscardLoadWithoutAnyFileKeepsDraftAndAllowsSaving()
    {
        var store = new MemoryStore();
        var session = new StudentSession(TestData.Catalog, "demo-001", store);
        session.SetSelection("D-BF", [1, 2]);
        await session.LoadAsync(discardChanges: true);
        Assert.False(session.LoadFailed); Assert.Single(session.Choice.Selections);
        await session.SaveAsync();
        Assert.Equal(1, store.Writes);
    }
    [Fact] public void CatalogRuleViolationBlocksOfferWithRuleId()
    {
        var offer = TestData.Offer("INF-LF", "INF", CourseType.Lf);
        var session = new StudentSession(TestData.Catalog with { Subjects = [new("INF", "Informatik")], Offers = [offer] }, "demo-001", new MemoryStore());
        Assert.False(session.Availability(offer).CanSelect);
        var error = Assert.Throws<InvalidOperationException>(() => session.SetSelection(offer.Id, [1, 2, 3, 4]));
        Assert.Contains("CATALOG-", error.Message);
    }
    [Fact] public void ProductionCatalogRejected() => Assert.Throws<ArgumentException>(() =>
        new StudentSession(TestData.Catalog with { IsDemo = false }, "demo-001", new MemoryStore()));

    private sealed class MemoryStore : IChoiceStore
    {
        public StoredChoice? Value { get; set; }
        public bool FailSave { get; init; }
        public int Writes { get; private set; }
        public string? LastExpectedToken { get; private set; }
        public Task<StoredChoice?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public Task<StoredChoice> SaveAsync(CourseChoice choice, string? expectedToken, CancellationToken cancellationToken = default)
        {
            if (FailSave) throw new IOException("synthetic failure");
            LastExpectedToken = expectedToken;
            Writes++; Value = new(choice, Writes.ToString()); return Task.FromResult(Value);
        }
    }
}
