using System.Security.Cryptography;
using System.Text.Json;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Storage;

/// <summary>Read-only boundary. Verification uses the original bytes, never reserialized JSON.</summary>
public sealed class SignedProfileReader(ProfileTrust trust, ICurrentIdentityProvider identity)
{
    public ProfileContext Read(string directory, string studentId, int graduationYear)
    {
        try
        {
            return Verify(ReadBounded(Path.Combine(directory, "profil.json")),
                ReadBounded(Path.Combine(directory, "profil.sig")), studentId, graduationYear);
        }
        catch (FileNotFoundException) { return ProfileContext.Missing with { Detail = "Profildatei oder Signatur fehlt. Oberstufenberatung kontaktieren." }; }
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

    public ProfileContext Verify(byte[] profileBytes, byte[] signatureBytes, string studentId, int graduationYear)
    {
        try
        {
            if (profileBytes.Length > 65536 || signatureBytes.Length > 65536 || trust.SchemaVersion != 1
                || trust.Keys.IsDefault || trust.AllowedAccountDomains.IsDefault
                || trust.Keys.Any(k => k is null || string.IsNullOrWhiteSpace(k.KeyId) || string.IsNullOrWhiteSpace(k.SubjectPublicKeyInfo)))
                return Invalid("Profil oder Vertrauenskonfiguration ungültig.");
            RejectDuplicates(signatureBytes);
            var signature = JsonSerializer.Deserialize<ProfileSignature>(signatureBytes, JsonDocuments.Options);
            if (signature is null || signature.SchemaVersion != 1 || signature.Algorithm != "ECDSA-P256-SHA256-P1363")
                return Invalid("Signaturschema oder Algorithmus nicht unterstützt.");
            var keys = trust.Keys.Where(k => k.KeyId == signature.KeyId).ToArray();
            if (keys.Length != 1) return Invalid("Unbekannter oder mehrdeutiger Prüfschlüssel.");
            using var key = ECDsa.Create();
            var publicBytes = Convert.FromBase64String(keys[0].SubjectPublicKeyInfo);
            key.ImportSubjectPublicKeyInfo(publicBytes, out var consumed);
            if (consumed != publicBytes.Length || key.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7")
                return Invalid("Prüfschlüssel ist kein P-256-Schlüssel.");
            if (!key.VerifyData(profileBytes, Convert.FromBase64String(signature.Signature), HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) return Invalid("Signatur ungültig; Profil möglicherweise verändert.");
            RejectDuplicates(profileBytes);
            var profile = JsonSerializer.Deserialize<StudentProfile>(profileBytes, JsonDocuments.Options);
            if (profile is null || profile.SchemaVersion != 1 || profile.ProfileRevision < 1 || profile.IssuedAt == default
                || profile.IssuedAt > DateTimeOffset.UtcNow.AddMinutes(5) || profile.KeyId != signature.KeyId)
                return Invalid("Profilschema, Revision, Ausstellungszeit oder Schlüsselbezug ungültig.");
            var account = identity.GetCurrent();
            var domain = account.Username.Split('\\');
            if (domain.Length != 2 || !trust.AllowedAccountDomains.Contains(domain[0], StringComparer.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(account.AccountId) || string.IsNullOrWhiteSpace(profile.AccountId)
                || account.AccountId != profile.AccountId
                || !string.Equals(account.Username, profile.WindowsUsername, StringComparison.OrdinalIgnoreCase)
                || profile.StudentId != studentId || profile.GraduationYear != graduationYear)
                return Invalid("Schulische Kontozuordnung, Schüler-ID oder Abiturjahrgang stimmt nicht überein.");
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
                Detail = "Signatur und Betriebssystemkonto geprüft. Verbindliche Prüfung erfolgt gegen den aktuellen Lehrerbestand."
            };
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or FormatException or ArgumentException
            or InvalidOperationException or System.Security.SecurityException or PlatformNotSupportedException or IOException or UnauthorizedAccessException)
        { return Invalid("Profil, Signatur oder Kontoprüfung ungültig. Oberstufenberatung kontaktieren."); }
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
