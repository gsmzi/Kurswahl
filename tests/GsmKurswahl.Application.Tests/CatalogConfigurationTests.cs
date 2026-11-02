using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Application.Tests;

public class CatalogConfigurationTests
{
    [Fact]
    public void PreparationRequiresNewVersionAndPreservesOriginal()
    {
        var basis = TestData.WorkflowCatalog;
        var prepared = CatalogConfiguration.Prepare(basis, 2030, "demo-2030-v1", new HashSet<string> { "D-LF", "M-LF", "E-LF" });
        Assert.Equal(2030, prepared.GraduationYear);
        Assert.Equal(OfferStatus.Planned, prepared.Offers.Single(o => o.Id == "W-LF").Status);
        Assert.Equal(OfferStatus.Offered, basis.Offers.Single(o => o.Id == "W-LF").Status);
        Assert.Throws<ArgumentException>(() => CatalogConfiguration.Prepare(basis, 2029, basis.Version, new HashSet<string>()));
        Assert.Throws<ArgumentException>(() => CatalogConfiguration.Prepare(basis, 2029, "new", new HashSet<string> { "INF-LF" }));
    }

    [Fact]
    public void OptionalBasicCoursesAndCustomFutureSubjectsAreSupportedWithoutSchemaChange()
    {
        var basis = TestData.WorkflowCatalog with
        {
            Subjects = [.. TestData.WorkflowCatalog.Subjects, new("NWT", "Naturwissenschaft und Technik") { TaskField = 3, IsScience = true }, new("SPA", "Spanisch") { TaskField = 1, IsForeignLanguage = true }],
            Offers = [.. TestData.WorkflowCatalog.Offers, TestData.Offer("NWT-BF", "NWT") with { Status = OfferStatus.Planned }, TestData.Offer("SPA-BF", "SPA")]
        };
        var prepared = CatalogConfiguration.Prepare(basis, 2031, "demo-2031-v1", new HashSet<string> { "D-LF", "M-LF", "E-LF" }, new HashSet<string> { "NWT-BF", "SPA-BF", "BIO-BF", "CH-BF" });
        Assert.Equal(OfferStatus.Offered, prepared.Offers.Single(o => o.Id == "NWT-BF").Status);
        var profile = new ProfileContext(ProfileStatus.Verified, PriorEducation.Unknown with { ContinuedLanguages = ["E", "SPA"] });
        var session = new StudentSession(prepared, "demo-001", new EmptyChoiceStore(), profile);
        foreach (var lf in new[] { "D-LF", "M-LF", "E-LF" }) session.SetSelection(lf, [1, 2, 3, 4]);
        session.NextStep();
        Assert.Contains("NWT", session.OralCandidates);
        Assert.Contains("SPA", session.OralCandidates);
    }

    private sealed class EmptyChoiceStore : IChoiceStore
    {
        public Task<StoredChoice?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<StoredChoice?>(null);
        public Task<StoredChoice> SaveAsync(CourseChoice choice, string? expectedToken, CancellationToken cancellationToken = default) => Task.FromResult(new StoredChoice(choice, "1"));
    }
}
