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
    [Theory] [InlineData("INF")] [InlineData("I")] [InlineData("ETH")] [InlineData("ER")] [InlineData("KR")]
    public void GsmForbiddenLfRejectedAtConfigurationBoundary(string subject)
    {
        // Source: Pflichtenheft Abschnitt 4, kein rechtlicher Volltest.
        Assert.Throws<ArgumentException>(() => ModelGuard.Catalog(TestData.Catalog with
        { Subjects = [new(subject, subject)], Offers = [TestData.Offer(subject, subject, CourseType.Lf)] }));
    }
}
