using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GsmKurswahl.Domain;

/// <summary>Current document versions. Writers always emit these; readers accept every listed older version.</summary>
public static class SchemaVersions
{
    public const int Choice = 2;
    public const int Catalog = 2;
    public const int Profile = 2;
    public const int StudentRecord = 2;
}

/// <summary>
/// Compatibility contract (since 02.11.2026): every released schema version stays readable. Older versions are
/// converted in memory to the current model; stored files and append-only database rows are never rewritten here.
/// Versions newer than this program are rejected. A structural change requires a new version, an explicit
/// upgrade below, a schema file and a frozen compatibility fixture.
/// </summary>
public static class VersionedDocuments
{
    internal static ImmutableArray<JsonConverter> Converters { get; } =
    [
        new VersionedConverter<CourseChoice>("Wahl", SchemaVersions.Choice, c => c.SchemaVersion, new()
        {
            // v1 (bis 25.10.2026): ohne plannedOralSubjects; keine Planung bedeutet leere Liste.
            [1] = e => e.Deserialize<ChoiceV1>(DocumentJson.Plain)?.Upgrade(),
        }),
        new VersionedConverter<OfferCatalog>("Katalog", SchemaVersions.Catalog, c => c.SchemaVersion, new()
        {
            // v1: Fächer ohne taskField/isForeignLanguage/isScience (Zwischenstand 02.11.2026 tolerierte sie optional).
            [1] = e => e.Deserialize<OfferCatalog>(DocumentJson.Plain) is { } v1 ? v1 with { SchemaVersion = SchemaVersions.Catalog } : null,
        }),
        new VersionedConverter<StudentProfile>("Profil", SchemaVersions.Profile, p => p.SchemaVersion, new()
        {
            // v1: mit den entfallenen Signaturfeldern keyId/accountId, ohne displayName/className
            // (Zwischenstand 02.11.2026 tolerierte diese optional). Die Signaturfelder hatten keine Wirkung.
            [1] = e => e.Deserialize<ProfileV1>(DocumentJson.Plain)?.Upgrade(),
        }),
        new VersionedConverter<StudentRecord>("Stammdaten", SchemaVersions.StudentRecord, r => r.SchemaVersion, new()
        {
            // v1: Schüler ohne firstName/lastName (Zwischenstand 02.11.2026 tolerierte sie optional).
            [1] = e => e.Deserialize<StudentRecord>(DocumentJson.Plain) is { } v1 ? v1 with { SchemaVersion = SchemaVersions.StudentRecord } : null,
        }),
    ];

    private sealed record ChoiceV1(int SchemaVersion, Guid DocumentId, string StudentId, int GraduationYear,
        string OfferVersion, string RuleVersion, int Revision, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
        SubmissionStatus SubmissionStatus, ElectionPhase Phase, ImmutableArray<CourseSelection> Selections)
    {
        public CourseChoice Upgrade() => new(SchemaVersions.Choice, DocumentId, StudentId, GraduationYear, OfferVersion,
            RuleVersion, Revision, CreatedAt, UpdatedAt, SubmissionStatus, Phase, Selections, []);
    }

    private sealed record ProfileV1(int SchemaVersion, int ProfileRevision, string StudentId, string WindowsUsername,
        int GraduationYear, DateTimeOffset IssuedAt, string KeyId, PriorEducation Prerequisites, string AccountId)
    {
        public string DisplayName { get; init; } = "";
        public string ClassName { get; init; } = "";
        public StudentProfile Upgrade() => new(SchemaVersions.Profile, ProfileRevision, StudentId, WindowsUsername,
            GraduationYear, IssuedAt, Prerequisites) { DisplayName = DisplayName, ClassName = ClassName };
    }

    private sealed class VersionedConverter<T>(string name, int current, Func<T, int> version,
        Dictionary<int, Func<JsonElement, T?>> upgrades) : JsonConverter<T> where T : class
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var property)
                || property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var found))
                throw new JsonException($"{name}: Schemaversion fehlt oder ist ungültig.");
            if (found == current) return root.Deserialize<T>(DocumentJson.Plain);
            if (upgrades.TryGetValue(found, out var upgrade)) return upgrade(root);
            throw new JsonException($"{name}: Schemaversion {found} wird von diesem Programm nicht unterstützt (aktuell {current}). Datei bleibt unverändert.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (version(value) != current)
                throw new JsonException($"{name}: Es wird ausschließlich Schemaversion {current} geschrieben.");
            JsonSerializer.Serialize(writer, value, DocumentJson.Plain);
        }
    }
}
