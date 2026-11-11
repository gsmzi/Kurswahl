using System.Text.Json;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Application.Tests;

public sealed class CourseAnnotationTests
{
    [Theory]
    [InlineData(CooperationSchool.None, true)]
    [InlineData(CooperationSchool.Ewg, false)]
    [InlineData(CooperationSchool.Gsm, true)]
    public void MarkersDoNotChangeEligibilityChoiceVersionOrDemand(CooperationSchool school, bool topUp)
    {
        var basis = TestData.WorkflowCatalog;
        var choice = TestData.Choice with { Selections = [.. basis.Offers.Where(o => o.Type == CourseType.Bf).Select(TestData.Select)] };
        var next = CatalogConfiguration.Annotate(basis, "BIO-BF", school, topUp);
        Assert.Equal(basis.Version, next.Version);
        Assert.Equal(CooperationSchool.None, basis.Offers.Single(o => o.Id == "BIO-BF").CooperationSchool);
        var validator = new ChoiceValidator();
        var before = validator.Validate(choice, basis, ProfileContext.Missing);
        var after = validator.Validate(choice, next, ProfileContext.Missing);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        Assert.Equal(validator.GetAvailability(basis.Offers.Single(o => o.Id == "BIO-BF"), ProfileContext.Missing),
            validator.GetAvailability(next.Offers.Single(o => o.Id == "BIO-BF"), ProfileContext.Missing));
        Assert.Contains("BIO", PlanningPresentation.Choices(choice, false, next));
        Assert.Equal(topUp, PlanningPresentation.Choices(choice, false, next).Contains("Aufsetzerkurs"));
        Assert.Equal(school, next.Offers.Single(o => o.Id == "BIO-BF").CooperationSchool);
        Assert.All(next.Offers.Where(o => o.Id != "BIO-BF"), o => Assert.Equal(basis.Offers.Single(b => b.Id == o.Id), o));
    }
}
