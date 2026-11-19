using System.Collections.Immutable;
using System.Text;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.ImportExport.Tests;

public class ControlSheetTests
{
    [Fact] public void ExportsActualPdfWithoutBrowserOrPrintPipeline()
    {
        var profile = new ProfileContext(ProfileStatus.Verified, PriorEducation.Unknown) { Revision = 2, DisplayName = "Demo Muster", ClassName = "10a" };
        var bytes = ControlSheet.Render(TestData.Choice, TestData.Catalog, profile,
            new(ReviewStatus.NeedsReview, []), [new(1, 1, 3)], []);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes.AsSpan(0, 8)));
        Assert.Contains("%%EOF", Encoding.ASCII.GetString(bytes.AsSpan(Math.Max(0, bytes.Length - 64))));
        Assert.True(bytes.Length > 1000);
    }

    [Fact] public void LongLabelsAndManyReviewMessagesProduceCompletePdf()
    {
        var catalog = TestData.Catalog with { Subjects = [new("D", "Deutsch / Französisch / Schülerübersicht ÄÖÜäöüß " + new string('W', 140))] };
        var choice = TestData.Choice with { Selections = [TestData.Select(TestData.Offer())] };
        var rule = new RuleDefinition("DEMO-REVIEW", "Synthetischer Test", "Synthetische Testquelle", "Test", "test", ReviewStatus.NeedsReview);
        var messages = Enumerable.Range(1, 18).Select(i => new RuleMessage(rule, [], [],
            $"Prüfhinweis {i}: Eine Fremdsprache und eine Naturwissenschaft sind Pflicht. Zusätzlich wird eine zweite Fremdsprache oder eine zweite Naturwissenschaft gewählt.",
            "Offene Voraussetzungen mit der Oberstufenberatung klären.")).ToImmutableArray();
        var bytes = ControlSheet.Render(choice, catalog, ProfileContext.Missing, new(ReviewStatus.NeedsReview, messages),
            Enumerable.Range(1, 4).Select(h => new SemesterSummary(h, 1, 3)), [new("F", "G", 36, ReviewStatus.NeedsReview, "Test")]);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes.AsSpan(0, 8)));
        // Explicit opt-in only: enables external structural/text/render verification of this exact production renderer.
        if (Environment.GetEnvironmentVariable("GSM_TEST_PDF_OUTPUT") is { Length: > 0 } path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllBytes(path, bytes);
        }
    }

    [Fact] public void ControlSheetHandlesTechnicalRuleMessagesAndLongLabelsCleanly()
    {
        var catalog = TestData.WorkflowCatalog with
        {
            Subjects = [.. TestData.WorkflowCatalog.Subjects.Select(s => s with { Name = s.Id == "GEO" ? "Geographie" : s.Name })]
        };
        var ruleOral = new RuleDefinition("PLAN-ORAL", "Mündlich", "Quelle", "Test", "m2-2", ReviewStatus.Invalid);
        var ruleReq = new RuleDefinition("AGVO-REQUIRED", "Pflicht", "Quelle", "Test", "m2-2", ReviewStatus.Invalid);
        var ruleTrust = new RuleDefinition("PROFILE-TRUST", "Profil", "Quelle", "Test", "m2-2", ReviewStatus.NeedsReview);
        var choice = TestData.Choice with { Selections = [TestData.Select(catalog.Offers.Single(o => o.Id == "D-LF"))] };
        var messages = ImmutableArray.Create(
            new RuleMessage(ruleTrust, [], [], "Kein verifiziertes persönliches Profil; Profilstatus: Missing", "Klärung"),
            new RuleMessage(ruleOral, [], [], "PLAN-ORAL: Deutsch, Mathematik und alle drei Aufgabenfelder müssen abgedeckt sein.", "Planen"),
            new RuleMessage(ruleReq, ["GEO"], [3, 4], "GEO: Pflichtbelegung fehlt.", "Belegen"));
        var bytes = ControlSheet.Render(choice, catalog, ProfileContext.Missing, new(ReviewStatus.Invalid, messages),
            Enumerable.Range(1, 4).Select(h => new SemesterSummary(h, 1, 5)), []);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes.AsSpan(0, 8)));
        Assert.Contains("%%EOF", Encoding.ASCII.GetString(bytes.AsSpan(Math.Max(0, bytes.Length - 64))));
    }
}
