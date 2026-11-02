using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using GsmKurswahl.Domain;

namespace GsmKurswahl.AdministrationStorage;

/// <summary>Versioned local workspace. Missing registered databases are errors, never empty replacements.</summary>
public sealed class TeacherWorkspace
{
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GSM-Kurswahl", "Lehrerverwaltung");
    public string Root { get; }
    private readonly string path;
    private string? token;
    public TeacherWorkspaceDocument Document { get; private set; }
    public TeacherYear? Active => Document.Years.SingleOrDefault(y => y.Catalog.GraduationYear == Document.ActiveYear);
    public TeacherWorkspace(string root)
    {
        Root = Path.GetFullPath(root);
        if (Root.StartsWith(@"\\", StringComparison.Ordinal) || new DriveInfo(Path.GetPathRoot(Root)!).DriveType == DriveType.Network)
            throw new ArgumentException("Verwaltungsverzeichnis muss lokal liegen.");
        path = Path.Combine(Root, "workspace.json");
        var bytes = Read(path);
        Document = bytes is null ? new(CurrentSchema, null, []) : Parse(bytes);
        token = Token(bytes);
    }
    public const int CurrentSchema = 2;
    // v1 carried the former certificate fields of each year. They never had an effect and are dropped in memory;
    // the file itself is only rewritten (as v2) by the next deliberate change.
    internal static byte[] UpgradeYears(byte[] bytes, string container)
    {
        var root = JsonNode.Parse(bytes, documentOptions: new() { MaxDepth = 32 }) as JsonObject ?? throw new InvalidDataException($"{container}: ungültiges JSON.");
        var version = root["schemaVersion"]?.GetValueKind() == JsonValueKind.Number ? root["schemaVersion"]!.GetValue<int>() : 0;
        if (version is not (1 or CurrentSchema)) throw new InvalidDataException($"{container}: Schemaversion {version} wird nicht unterstützt; keine Migration.");
        if (version == CurrentSchema) return bytes;
        static void Strip(JsonNode? year)
        {
            if (year is not JsonObject value) return;
            foreach (var legacy in new[] { "certificateThumbprint", "keyId" })
            {
                if (!value.ContainsKey(legacy) || value[legacy]?.GetValueKind() != JsonValueKind.String) throw new InvalidDataException("Jahrgangseintrag v1 unvollständig.");
                value.Remove(legacy);
            }
        }
        if (root["years"] is JsonArray years) foreach (var year in years) Strip(year);
        if (root["year"] is { } single) Strip(single);
        root["schemaVersion"] = CurrentSchema;
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }
    private static TeacherWorkspaceDocument Parse(byte[] bytes)
    {
        var value = JsonSerializer.Deserialize<TeacherWorkspaceDocument>(UpgradeYears(bytes, "Jahrgangsverwaltung"), DocumentJson.Options) ?? throw new InvalidDataException("Leere Jahrgangsverwaltung.");
        if (value.SchemaVersion != CurrentSchema || value.Years.IsDefault || (value.Years.Length > 0 && value.ActiveYear is null) || value.Years.Select(y => y.Catalog.GraduationYear).Distinct().Count() != value.Years.Length
            || (value.ActiveYear is not null && !value.Years.Any(y => y.Catalog.GraduationYear == value.ActiveYear)))
            throw new InvalidDataException("Unbekannte oder ungültige Jahrgangsverwaltung; keine automatische Migration.");
        foreach (var year in value.Years)
        {
            ModelGuard.Catalog(year.Catalog);
            if (!year.Catalog.IsDemo || !Enum.IsDefined(year.Step) || !Path.IsPathFullyQualified(year.DatabasePath) || string.IsNullOrWhiteSpace(year.Actor))
                throw new InvalidDataException("Ungültige Jahrgangseinstellungen.");
        }
        return value;
    }
    private static byte[]? Read(string file)
    {
        try
        {
            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length > 4 * 1024 * 1024) throw new InvalidDataException("Jahrgangsverwaltung zu groß.");
            var bytes = new byte[(int)input.Length]; input.ReadExactly(bytes); return bytes;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
    private static string? Token(byte[]? bytes) => bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes));
    private void Save(TeacherWorkspaceDocument value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, DocumentJson.Options); _ = Parse(bytes);
        Directory.CreateDirectory(Root);
        using var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (Token(Read(path)) != token) throw new IOException("Jahrgangsverwaltung inzwischen geändert. Anwendung neu öffnen.");
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { output.Write(bytes); output.Flush(true); }
            if (token is null) File.Move(temp, path); else File.Replace(temp, path, null);
            Document = value; token = Token(bytes);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Create(OfferCatalog catalog, string actor)
    {
        if (Document.Years.Any(y => y.Catalog.GraduationYear == catalog.GraduationYear)) throw new InvalidOperationException("Dieser Jahrgang ist bereits eingerichtet. Bitte auswählen.");
        var database = Path.Combine(Root, "Abitur-" + catalog.GraduationYear, "verwaltung.db");
        if (File.Exists(database)) throw new IOException("Vorhandene Verwaltungsdatei ohne Zuordnung. Über Einstellungen ausdrücklich einbinden.");
        var year = new TeacherYear(catalog, database, YearSetupStep.Offer, actor);
        // Validate all values before creating any database.
        var next = Document with { ActiveYear = catalog.GraduationYear, Years = Document.Years.Add(year) };
        _ = Parse(JsonSerializer.SerializeToUtf8Bytes(next, DocumentJson.Options));
        using (var created = new SqliteAdministrationStore(database)) { }
        Save(next);
    }
    public SqliteAdministrationStore OpenActive()
    {
        var year = Active ?? throw new InvalidOperationException("Kein aktiver Jahrgang.");
        if (!File.Exists(year.DatabasePath)) throw new FileNotFoundException("Der gespeicherte Lehrerbestand fehlt. Speicherort prüfen; es wird kein leerer Ersatz angelegt.", year.DatabasePath);
        return new SqliteAdministrationStore(year.DatabasePath);
    }
    public void Update(TeacherYear year)
    {
        var index = Enumerable.Range(0, Document.Years.Length).FirstOrDefault(i => Document.Years[i].Catalog.GraduationYear == year.Catalog.GraduationYear, -1);
        if (index < 0 || year.DatabasePath != Document.Years[index].DatabasePath) throw new InvalidOperationException("Unbekannter oder abweichender Jahrgang.");
        Save(Document with { Years = Document.Years.SetItem(index, year) });
    }
    public void Activate(int year)
    {
        if (!Document.Years.Any(y => y.Catalog.GraduationYear == year)) throw new ArgumentException("Jahrgang nicht vorhanden.");
        Save(Document with { ActiveYear = year });
    }
    public void Attach(OfferCatalog catalog, string databasePath, string actor)
    {
        if (!File.Exists(databasePath)) throw new FileNotFoundException("Vorhandene Verwaltungsdatei auswählen.");
        if (Document.Years.Any(y => y.Catalog.GraduationYear == catalog.GraduationYear)) throw new InvalidOperationException("Jahrgang bereits vorhanden; bestehende Zuordnung bleibt erhalten.");
        using var existing = new SqliteAdministrationStore(databasePath);
        var students = existing.Students();
        if (students.Any(s => s.Student.GraduationYear != catalog.GraduationYear))
            throw new InvalidDataException("Die ausgewählte Verwaltungsdatei enthält Schüler eines anderen Abiturjahrgangs.");
        var year = new TeacherYear(catalog, Path.GetFullPath(databasePath), YearSetupStep.Import, actor);
        Save(Document with { ActiveYear = catalog.GraduationYear, Years = Document.Years.Add(year) });
    }
}
