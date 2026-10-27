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
}
