using GsmKurswahl.Domain;

namespace GsmKurswahl.Application;

public static class StudentImportService
{
    private static StudentRecord KeepResolvedDirectory(StudentRecord current, StudentRecord candidate)
    {
        if (!string.IsNullOrWhiteSpace(current.AccountId) && string.IsNullOrWhiteSpace(candidate.AccountId)
            && string.Equals(current.WindowsUsername, candidate.WindowsUsername, StringComparison.OrdinalIgnoreCase))
            candidate = candidate with { AccountId = current.AccountId };
        var relative = candidate.HomeDirectory.Replace('\\', '/');
        if (Path.IsPathFullyQualified(current.HomeDirectory) && !Path.IsPathRooted(candidate.HomeDirectory)
            && (relative.Length == 0 || (!relative.Split('/').Any(p => p is ".." or "." or "")
                && current.HomeDirectory.Replace('\\', '/').EndsWith("/" + relative, StringComparison.OrdinalIgnoreCase))))
            return candidate with { HomeDirectory = current.HomeDirectory };
        return candidate;
    }
    public static string Differences(StudentRecord current, StudentRecord candidate)
    {
        candidate = KeepResolvedDirectory(current, candidate);
        var differences = new List<string>();
        if (current.Student.SyntheticDisplayName != candidate.Student.SyntheticDisplayName) differences.Add("Name");
        if (current.Student.ClassName != candidate.Student.ClassName) differences.Add("Klasse");
        if (current.Student.GraduationYear != candidate.Student.GraduationYear) differences.Add("Jahrgang");
        if (current.Student.Departure != candidate.Student.Departure) differences.Add("Abgängerstatus");
        if (current.WindowsUsername != candidate.WindowsUsername || current.AccountId != candidate.AccountId) differences.Add("Konto/SID");
        if (current.HomeDirectory != candidate.HomeDirectory) differences.Add("Ordner");
        if (System.Text.Json.JsonSerializer.Serialize(current.Student.Prerequisites, DocumentJson.Options) != System.Text.Json.JsonSerializer.Serialize(candidate.Student.Prerequisites, DocumentJson.Options)) differences.Add("Voraussetzungen");
        return string.Join(", ", differences);
    }
    public static bool Confirm(IAdministrationStore store, StudentRecord candidate, int? expectedVersion, bool confirmed)
    {
        if (!confirmed) throw new InvalidOperationException("Stammdatenimport benötigt bestätigte Vorschau.");
        var incomingOrigin = candidate.Origin ?? throw new ArgumentException("Importherkunft fehlt.");
        var current = store.Students().SingleOrDefault(s => s.Student.ExternalStudentId == candidate.Student.ExternalStudentId);
        if (current is not null) candidate = KeepResolvedDirectory(current, candidate);
        if (current?.Version != expectedVersion) throw new InvalidOperationException("Stammdaten inzwischen geändert; Vorschau erneuern.");
        if (current?.Origin is { } origin && origin.ContentHash == incomingOrigin.ContentHash && origin.MappingVersion == incomingOrigin.MappingVersion) return false;
        if (current is not null && Differences(current, candidate).Length == 0) return false;
        store.SaveStudent(candidate with
        {
            Student = candidate.Student with { Id = current?.Student.Id ?? candidate.Student.Id },
            Version = (expectedVersion ?? 0) + 1,
            MappingConfirmed = current?.MappingConfirmed == true && current.WindowsUsername == candidate.WindowsUsername && current.AccountId == candidate.AccountId && current.HomeDirectory == candidate.HomeDirectory
        }, expectedVersion);
        return true;
    }
}
