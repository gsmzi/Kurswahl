using System.Text.Json;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Storage.Tests;

public sealed class CourseAnnotationTests
{
    [Fact]
    public void CurrentCatalogRejectsMissingMarkersUnknownSchoolAndFutureSchema()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(TestData.Catalog, DocumentJson.Options))!;
        var offer = node["offers"]![0]!;
        offer.AsObject().Remove("isTopUpCourse");
        Assert.Throws<JsonException>(() => JsonDocuments.ReadCatalog(JsonSerializer.SerializeToUtf8Bytes(node)));
        offer["isTopUpCourse"] = false; offer["cooperationSchool"] = "unbekannt";
        Assert.Throws<JsonException>(() => JsonDocuments.ReadCatalog(JsonSerializer.SerializeToUtf8Bytes(node)));
        offer["cooperationSchool"] = "none"; node["schemaVersion"] = 4;
        Assert.Throws<JsonException>(() => JsonDocuments.ReadCatalog(JsonSerializer.SerializeToUtf8Bytes(node)));
    }
    [Fact]
    public void MarkersSurviveCancellationAndReopeningWithoutChangingPupilHistory()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-markers-" + Guid.NewGuid());
        try
        {
            var workspace = new TeacherWorkspace(root); workspace.Create(TestData.WorkflowCatalog, "demo-beratung");
            using (var store = workspace.OpenActive())
            {
                var before = CoursePlanning.Capture(store, workspace.Active!.Catalog, "demo-beratung", "Test", true);
                var cancellation = CoursePlanning.PreviewCancellation(before, ["BIO-LF"]);
                CoursePlanning.Confirm(store, cancellation, "demo-beratung", "Synthetischer Test", true);
                var next = CatalogConfiguration.Annotate(cancellation.Catalog, "CH-LF", CooperationSchool.Ewg, true);
                store.SaveCourseAnnotations(cancellation.Catalog, next, "demo-beratung");
                Assert.Empty(store.History("demo-001"));
                Assert.Throws<InvalidOperationException>(() => store.SaveCourseAnnotations(cancellation.Catalog, next, "demo-beratung"));
                Assert.Throws<ArgumentException>(() => store.SaveCourseAnnotations(next, next with { Version = "changed" }, "demo-beratung"));
                Assert.Equal(OfferStatus.Cancelled, store.ActivePlanningCatalog()!.Offers.Single(o => o.Id == "BIO-LF").Status);
            }
            using var reopened = new TeacherWorkspace(root).OpenActive();
            var saved = reopened.ActivePlanningCatalog()!;
            Assert.Equal(CooperationSchool.Ewg, saved.Offers.Single(o => o.Id == "CH-LF").CooperationSchool);
            Assert.True(saved.Offers.Single(o => o.Id == "CH-LF").IsTopUpCourse);
            Assert.Equal(OfferStatus.Cancelled, saved.Offers.Single(o => o.Id == "BIO-LF").Status);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("catalog-v2.json")]
    [InlineData("workspace-v2-catalog-v2.json")]
    [InlineData("transfer-v2-catalog-v2.json")]
    [InlineData("planning-document-catalog-v2.json")]
    public void FrozenV2CatalogsIncludingNestedDocumentsStayReadable(string name)
    {
        var path = Path.Combine(Path.GetDirectoryName(GetSourcePath())!, "Compatibility", "legacy", name);
        var bytes = File.ReadAllBytes(path);
        OfferCatalog catalog = name switch
        {
            "catalog-v2.json" => JsonDocuments.ReadCatalog(bytes),
            "workspace-v2-catalog-v2.json" => JsonSerializer.Deserialize<TeacherWorkspaceDocument>(bytes, DocumentJson.Options)!.Years.Single().Catalog,
            "transfer-v2-catalog-v2.json" => JsonSerializer.Deserialize<TransferManifest>(bytes, DocumentJson.Options)!.Year.Catalog,
            _ => JsonSerializer.Deserialize<PlanningDocument>(bytes, DocumentJson.Options)!.Catalog
        };
        Assert.Equal(SchemaVersions.Catalog, catalog.SchemaVersion);
        Assert.All(catalog.Offers, o => { Assert.Equal(CooperationSchool.None, o.CooperationSchool); Assert.False(o.IsTopUpCourse); });
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Contains("\"schemaVersion\": 3", JsonSerializer.Serialize(catalog, DocumentJson.Options));
    }
    private static string GetSourcePath([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
}
