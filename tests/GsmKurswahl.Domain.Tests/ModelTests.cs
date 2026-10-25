using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Domain.Tests;

public class ModelTests
{
    [Fact] public void DraftAndCatalogAreStructurallyValid()
    {
        ModelGuard.Choice(TestData.Choice);
        ModelGuard.Catalog(TestData.Catalog);
    }
    [Fact] public void UnknownPrerequisitesAreNotNoOrYes() => Assert.Equal(Knowledge.Unknown, PriorEducation.Unknown.InformaticsImp);
    [Fact] public void FutureSchemaIsRejected() => Assert.Throws<ArgumentException>(() => ModelGuard.Choice(TestData.Choice with { SchemaVersion = 2 }));
    [Theory] [InlineData(0)] [InlineData(5)]
    public void InvalidSemestersRejected(int semester) => Assert.Throws<ArgumentException>(() => ModelGuard.Choice(TestData.Choice with
    { Selections = [TestData.Select(TestData.Offer()) with { Semesters = [semester] }] }));
    [Fact] public void DuplicateOffersRejected() => Assert.Throws<ArgumentException>(() => ModelGuard.Catalog(TestData.Catalog with { Offers = [TestData.Offer(), TestData.Offer()] }));
    [Fact] public void StructuralGuardLeavesSchoolOfferRulesToRules()
    {
        // Schulische Angebotsgrenzen sind Regeln mit ID in GsmKurswahl.Rules, keine Strukturfehler.
        ModelGuard.Catalog(TestData.Catalog with { Subjects = [new("INF", "Informatik")], Offers = [TestData.Offer("INF-LF", "INF", CourseType.Lf)] });
    }
    [Fact] public void UnknownSubjectReferenceRejected() => Assert.Throws<ArgumentException>(() =>
        ModelGuard.Catalog(TestData.Catalog with { Offers = [TestData.Offer("X-BF", "X")] }));
}
