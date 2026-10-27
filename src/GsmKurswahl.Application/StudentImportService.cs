using GsmKurswahl.Domain;

namespace GsmKurswahl.Application;

public static class StudentImportService
{
    public static bool Confirm(IAdministrationStore store, StudentRecord candidate, int? expectedVersion, bool confirmed)
    {
        if (!confirmed) throw new InvalidOperationException("Stammdatenimport benötigt bestätigte Vorschau.");
        if (candidate.Origin is null) throw new ArgumentException("Importherkunft fehlt.");
        var current = store.Students().SingleOrDefault(s => s.Student.ExternalStudentId == candidate.Student.ExternalStudentId);
        if (current?.Version != expectedVersion) throw new InvalidOperationException("Stammdaten inzwischen geändert; Vorschau erneuern.");
        if (current?.Origin is { } origin && origin.ContentHash == candidate.Origin.ContentHash && origin.MappingVersion == candidate.Origin.MappingVersion) return false;
        store.SaveStudent(candidate with
        {
            Student = candidate.Student with { Id = current?.Student.Id ?? candidate.Student.Id },
            Version = (expectedVersion ?? 0) + 1,
            MappingConfirmed = false
        }, expectedVersion);
        return true;
    }
}
