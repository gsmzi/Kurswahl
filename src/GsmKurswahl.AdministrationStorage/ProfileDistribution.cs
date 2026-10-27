using System.Security.Cryptography;
using System.Text.Json;
using GsmKurswahl.Domain;

namespace GsmKurswahl.AdministrationStorage;

// Signing is called only by administrative composition. The pupil application has no key loader or signing UI.
public sealed class ProfileDistribution(IAdministrationStore store)
{
    public IssuedProfile Issue(StudentRecord record, ECDsa key, string keyId)
    {
        var current = store.Students().Single(s => s.Student.ExternalStudentId == record.Student.ExternalStudentId);
        if (current.Version != record.Version) throw new InvalidOperationException("Stammdaten geändert.");
        record = current;
        if (!record.MappingConfirmed || string.IsNullOrWhiteSpace(record.AccountId) || record.WindowsUsername.Split('\\').Length != 2
            || !Path.IsPathFullyQualified(record.HomeDirectory) || string.IsNullOrWhiteSpace(keyId))
            throw new ArgumentException("Bestätigte Schüler-/Konto-/SID-/Home-Zuordnung erforderlich.");
        if (key.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7") throw new ArgumentException("P-256-Schlüssel erforderlich.");
        var previous = store.LatestProfile(record.Student.ExternalStudentId);
        var profile = new StudentProfile(1, (previous?.Profile.ProfileRevision ?? 0) + 1, record.Student.ExternalStudentId,
            record.WindowsUsername, record.Student.GraduationYear, DateTimeOffset.UtcNow, keyId, record.Student.Prerequisites, record.AccountId);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(profile, DocumentJson.Options);
        if (bytes.Length > 65536) throw new ArgumentException("Profil überschreitet 64 KiB.");
        var signature = new ProfileSignature(1, keyId, "ECDSA-P256-SHA256-P1363", Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        var signed = new IssuedProfile(profile, bytes, JsonSerializer.SerializeToUtf8Bytes(signature, DocumentJson.Options));
        store.SaveProfile(signed);
        return signed;
    }
    public DistributionEntry Distribute(StudentRecord record, IssuedProfile issued)
    {
        var status = "verteilt"; var detail = "Einzelprofil verteilt; Schülerwahlen unverändert.";
        try
        {
            var current = store.Students().Single(s => s.Student.ExternalStudentId == record.Student.ExternalStudentId);
            if (current.Version != record.Version) throw new InvalidDataException("veraltet");
            record = current;
            var latest = store.LatestProfile(record.Student.ExternalStudentId);
            if (!record.MappingConfirmed || issued.Profile.StudentId != record.Student.ExternalStudentId
                || issued.Profile.WindowsUsername != record.WindowsUsername || issued.Profile.AccountId != record.AccountId
                || issued.Profile.GraduationYear != record.Student.GraduationYear
                || JsonSerializer.Serialize(issued.Profile.Prerequisites, DocumentJson.Options) != JsonSerializer.Serialize(record.Student.Prerequisites, DocumentJson.Options)
                || latest?.Profile.ProfileRevision != issued.Profile.ProfileRevision
                || !latest.ProfileBytes.SequenceEqual(issued.ProfileBytes) || !latest.SignatureBytes.SequenceEqual(issued.SignatureBytes))
                throw new InvalidDataException("veraltet");
            if (!Path.IsPathFullyQualified(record.HomeDirectory) || !Directory.Exists(record.HomeDirectory)) throw new DirectoryNotFoundException();
            using var lease = new FileStream(Path.Combine(record.HomeDirectory, ".profil.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var path = Path.Combine(record.HomeDirectory, "profil.json");
            var sigPath = Path.Combine(record.HomeDirectory, "profil.sig");
            var before = ReadExisting(path);
            var beforeSig = ReadExisting(sigPath);
            if (beforeSig is not null)
            {
                var oldSignature = JsonSerializer.Deserialize<ProfileSignature>(beforeSig, DocumentJson.Options);
                if (oldSignature is null || oldSignature.SchemaVersion != 1 || oldSignature.Algorithm != "ECDSA-P256-SHA256-P1363")
                    throw new InvalidDataException("Profil_abweichend");
                try { if (Convert.FromBase64String(oldSignature.Signature).Length != 64) throw new InvalidDataException("Profil_abweichend"); }
                catch (FormatException) { throw new InvalidDataException("Profil_abweichend"); }
            }
            if (before is not null)
            {
                var old = JsonSerializer.Deserialize<StudentProfile>(before, DocumentJson.Options);
                if (old is null || old.SchemaVersion != 1 || old.StudentId != issued.Profile.StudentId || old.WindowsUsername != issued.Profile.WindowsUsername || old.AccountId != issued.Profile.AccountId)
                    throw new InvalidDataException("Profil_abweichend");
                if (old.ProfileRevision > issued.Profile.ProfileRevision) throw new InvalidDataException("veraltet");
                if (old.ProfileRevision == issued.Profile.ProfileRevision && !before.SequenceEqual(issued.ProfileBytes)) throw new InvalidDataException("Profil_abweichend");
            }
            Replace(path, issued.ProfileBytes, before);
            // A crash between replacements yields an invalid signature, never a trusted mixed pair.
            Replace(sigPath, issued.SignatureBytes, beforeSig);
        }
        catch (DirectoryNotFoundException) { status = "nicht_gefunden"; detail = "Bestätigtes Zielverzeichnis nicht erreichbar."; }
        catch (UnauthorizedAccessException) { status = "Zugriff_verweigert"; detail = "Keine Schreibberechtigung am Ziel."; }
        catch (InvalidDataException ex) { status = ex.Message; detail = "Profil oder Verwaltungsstand abweichend; Vorschau erneuern und Zuordnung prüfen."; }
        catch (JsonException) { status = "Profil_abweichend"; detail = "Defektes vorhandenes Profil bleibt erhalten."; }
        catch (IOException) { status = "Zugriff_verweigert"; detail = "Ziel gesperrt, verändert oder Schreibvorgang fehlgeschlagen; Wiederholung erforderlich."; }
        var entry = new DistributionEntry(Guid.NewGuid(), record.Student.ExternalStudentId, issued.Profile.ProfileRevision, DateTimeOffset.UtcNow, status, detail);
        store.LogDistribution(entry); return entry;
    }
    private static byte[]? ReadExisting(string path)
    {
        if (!File.Exists(path)) return null;
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 65536) throw new InvalidDataException("Profil_abweichend");
        var bytes = new byte[(int)input.Length]; input.ReadExactly(bytes); return bytes;
    }
    private static void Replace(string path, byte[] bytes, byte[]? expected)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes); file.Flush(true); }
            var actual = File.Exists(path) ? File.ReadAllBytes(path) : null;
            if ((expected is null) != (actual is null) || (expected is not null && !expected.SequenceEqual(actual!))) throw new IOException("Konkurrenzänderung.");
            if (expected is null) File.Move(temp, path); else File.Replace(temp, path, null);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
