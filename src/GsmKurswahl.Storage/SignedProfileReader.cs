using System.Text.Json;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Storage;

/// <summary>Read-only boundary for student profiles (profil.json). Authoritative validation always happens in the teacher app against the local database.</summary>
public sealed class SignedProfileReader(ProfileTrust? trust = null, ICurrentIdentityProvider? identity = null)
{
    private readonly ProfileTrust? configuredTrust = trust;
    private readonly ICurrentIdentityProvider? identityProvider = identity;

    // Bootstrap an advisory identity from one explicitly selected personal directory, never from a shared roster.
    public (string StudentId, ProfileContext Profile) ReadPersonal(string directory, int graduationYear)
    {
        var bytes = ReadBounded(Path.Combine(directory, "profil.json"));
        RejectDuplicates(bytes);
        var profile = JsonSerializer.Deserialize<StudentProfile>(bytes, JsonDocuments.Options) ?? throw new JsonException("Profil fehlt.");
        if (!profile.StudentId.StartsWith("demo-", StringComparison.Ordinal)) throw new ArgumentException("Dieser Entwicklungsstand erlaubt nur synthetische demo-IDs.");
        var context = Verify(bytes, profile.StudentId, graduationYear);
        if (context.Status != ProfileStatus.Verified) throw new ArgumentException(context.Detail);
        return (profile.StudentId, context);
    }

    public ProfileContext Read(string directory, string studentId, int graduationYear)
    {
        try
        {
            return Verify(ReadBounded(Path.Combine(directory, "profil.json")), studentId, graduationYear);
        }
        catch (FileNotFoundException) { return ProfileContext.Missing with { Detail = "Profildatei fehlt. Oberstufenberatung kontaktieren." }; }
        catch (DirectoryNotFoundException) { return ProfileContext.Missing; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return Invalid("Profil nicht lesbar. Wahlentwurf bleibt erhalten."); }
    }

    private static byte[] ReadBounded(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 65536) throw new IOException("Profil zu groß.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    public ProfileContext Verify(byte[] profileBytes, string studentId, int graduationYear)
    {
        try
        {
            if (profileBytes.Length is 0 or > 65536)
                return Invalid("Profilgröße ungültig.");
            RejectDuplicates(profileBytes);
            var profile = JsonSerializer.Deserialize<StudentProfile>(profileBytes, JsonDocuments.Options);
            if (profile is null || profile.SchemaVersion != 1 || profile.ProfileRevision < 1 || profile.IssuedAt == default
                || profile.IssuedAt > DateTimeOffset.UtcNow.AddMinutes(5))
                return Invalid("Profilschema, Revision oder Ausstellungszeit ungültig.");
            if (profile.StudentId != studentId || profile.GraduationYear != graduationYear)
                return Invalid("Schüler-ID oder Abiturjahrgang stimmt nicht mit dem Profil überein.");
            if (identityProvider is not null && configuredTrust is { AllowedAccountDomains.IsDefaultOrEmpty: false }
                && !string.IsNullOrWhiteSpace(profile.WindowsUsername))
            {
                var account = identityProvider.GetCurrent();
                var domain = account.Username.Split('\\');
                if (domain.Length != 2 || domain.Any(string.IsNullOrWhiteSpace)
                    || !configuredTrust.AllowedAccountDomains.Contains(domain[0], StringComparer.OrdinalIgnoreCase)
                    || !string.Equals(account.Username, profile.WindowsUsername, StringComparison.OrdinalIgnoreCase))
                    return Invalid("Schulische Kontozuordnung stimmt nicht überein.");
            }
            var p = profile.Prerequisites;
            if (p is null || p.ContinuedLanguages.IsDefault || p.ContinuedLanguages.Any(string.IsNullOrWhiteSpace)
                || p.ContinuedLanguages.Distinct().Count() != p.ContinuedLanguages.Length
                || (p.SportExemptSemesters is { } semesters && (semesters.IsDefault || semesters.Any(h => h is < 1 or > 4)
                    || semesters.Distinct().Count() != semesters.Length))
                || (p.ReligionEthicsGrade10 is not null and not ("ETH" or "ER" or "KR")))
                return Invalid("Persönliche Angaben sind widersprüchlich oder ungültig.");
            return new(ProfileStatus.Verified, p)
            {
                Revision = profile.ProfileRevision, IssuedAt = profile.IssuedAt,
                Detail = "Persönliches Profil geladen. Verbindliche Prüfung erfolgt gegen den aktuellen Lehrerbestand."
            };
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException
            or InvalidOperationException or System.Security.SecurityException or PlatformNotSupportedException or IOException or UnauthorizedAccessException)
        { return Invalid("Profil ungültig. Oberstufenberatung kontaktieren."); }
    }

    private static ProfileContext Invalid(string detail) => new(ProfileStatus.ProfileInvalid, PriorEducation.Unknown) { Detail = detail };

    private static void RejectDuplicates(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        Visit(document.RootElement);
        static void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name)) throw new JsonException("Doppeltes JSON-Feld.");
                    Visit(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var child in element.EnumerateArray()) Visit(child);
        }
    }
}
