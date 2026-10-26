namespace GsmKurswahl.Storage.Tests;

public class SampleTests
{
    [Fact] public void ShippedCatalogAndDraftAreReadableAndConsistent()
    {
        var catalog = JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json")));
        var choice = JsonDocuments.ReadChoice(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "schueler-demo.json")));
        Assert.True(catalog.IsDemo); Assert.StartsWith("demo-", choice.StudentId);
        Assert.Equal(catalog.Version, choice.OfferVersion);
        Assert.All(choice.Selections, s => Assert.Contains(catalog.Offers, o => o.Id == s.OfferId && o.SubjectId == s.SubjectId));
        Assert.Contains(catalog.Offers, o => o.Id == "W-LF" && o.Status == Domain.OfferStatus.Offered);
        Assert.Empty(Rules.CatalogRules.Check(catalog));
        Assert.Equal(Domain.ReviewStatus.Invalid, new Rules.ChoiceValidator().Validate(choice, catalog, Domain.ProfileContext.Missing).Status);
    }
}
