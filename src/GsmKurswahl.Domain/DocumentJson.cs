using System.Text.Json;
using System.Text.Json.Serialization;

namespace GsmKurswahl.Domain;

// Common serialization contract; no filesystem or UI dependency.
public static class DocumentJson
{
    // Plain options bind exactly one schema version; Options adds the versioned document readers.
    internal static JsonSerializerOptions Plain { get; } = CreateOptions(versioned: false);
    public static JsonSerializerOptions Options { get; } = CreateOptions(versioned: true);
    private static JsonSerializerOptions CreateOptions(bool versioned)
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
        if (versioned)
            foreach (var converter in VersionedDocuments.Converters) options.Converters.Add(converter);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
