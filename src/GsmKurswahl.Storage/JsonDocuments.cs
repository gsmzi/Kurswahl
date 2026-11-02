using System.Text.Json;
using System.Text.Json.Serialization;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Storage;

public static class JsonDocuments
{
    public static JsonSerializerOptions Options => DocumentJson.Options;

    public static CourseChoice ReadChoice(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("schemaVersion", out var schema)
            && schema.ValueKind == JsonValueKind.Number && schema.TryGetInt32(out var version) && version is not (1 or SchemaVersions.Choice))
            throw new ArgumentException($"Nicht unterstützte Wahl-Schemaversion {version}; neuere Programmversion erforderlich. Datei bleibt erhalten.");
        var choice = JsonSerializer.Deserialize<CourseChoice>(bytes, Options) ?? throw new JsonException("Leere Wahl-Datei.");
        ModelGuard.Choice(choice);
        return choice;
    }
    // Older versions are converted in memory only; the file changes only on an explicit later save.
    public static int? SchemaVersionOf(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("schemaVersion", out var schema)
                && schema.ValueKind == JsonValueKind.Number && schema.TryGetInt32(out var version) ? version : null;
        }
        catch (JsonException) { return null; }
    }
    public static OfferCatalog ReadCatalog(byte[] bytes)
    {
        var catalog = JsonSerializer.Deserialize<OfferCatalog>(bytes, Options) ?? throw new JsonException("Leerer Katalog.");
        ModelGuard.Catalog(catalog);
        return catalog;
    }
    public static byte[] WriteChoice(CourseChoice choice)
    {
        ModelGuard.Choice(choice);
        return JsonSerializer.SerializeToUtf8Bytes(choice, Options);
    }
}
