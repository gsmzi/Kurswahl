using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.ImportExport.Tests;

public class ImportBoundaryTests
{
    [Fact] public void PreviewKeepsOriginAndRequiresConfirmationEvenWithoutConflicts()
    {
        var origin = new ImportOrigin("synthetic.csv", "demo-hash", "mapping-v1", DateTimeOffset.UtcNow, "demo-account");
        var preview = new ImportPreview(origin, [], [TestData.Choice], []);
        Assert.True(preview.RequiresConfirmation);
        Assert.Equal(origin, preview.Origin);
        Assert.Single(preview.Candidates);
    }
}
