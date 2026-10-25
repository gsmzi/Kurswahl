using System.Text.Json;
using System.Text.Json.Serialization;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Storage;

public static class JsonDocuments
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            RespectRequiredConstructorParameters = true,
            RespectNullableAnnotations = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 32
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    public static CourseChoice ReadChoice(byte[] bytes)
    {
        var choice = JsonSerializer.Deserialize<CourseChoice>(bytes, Options) ?? throw new JsonException("Leere Wahl-Datei.");
        ModelGuard.Choice(choice);
        return choice;
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
