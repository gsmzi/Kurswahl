using GsmKurswahl.Domain;

namespace GsmKurswahl.AdministrationStorage;

public static class StudentDirectories
{
    public sealed record DistributionTarget(StudentRecord Student, string? Path, bool RequiresReview, string Detail)
    {
        public string[] AvailableFolders { get; init; } = [];
    }

    public static DistributionTarget[] PreviewDistribution(string root, IReadOnlyList<StudentRecord> students)
    {
        if (!Path.IsPathFullyQualified(root))
            return students.Select(s => new DistributionTarget(s, null, false, "Bitte das vollständige Grundverzeichnis mit den Klassenordnern angeben.")).ToArray();
        if (!Directory.Exists(root))
            return students.Select(s => new DistributionTarget(s, null, false, "Grundverzeichnis nicht erreichbar: " + root)).ToArray();

        var classFolders = new Dictionary<string, DirectoryInfo[]>();
        var targets = students.Select(FindTarget).ToArray();
        var duplicates = targets.Where(t => t.Path is not null)
            .GroupBy(t => t.Path!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var checkedTargets = targets.Select(t => t.Path is not null && duplicates.Contains(t.Path)
            ? t with { Path = null, Detail = $"Ordner mehreren Personen zugeordnet: {t.Path}" }
            : t).ToArray();
        var assigned = checkedTargets.Where(t => t.Path is not null && !t.RequiresReview)
            .Select(t => t.Path!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stored = students.Where(s => Path.IsPathFullyQualified(s.HomeDirectory))
            .ToLookup(s => Path.TrimEndingDirectorySeparator(s.HomeDirectory), StringComparer.OrdinalIgnoreCase);
        return checkedTargets.Select(t => t.Path is null ? t with { AvailableFolders = FindFreeFolders(t.Student) } : t).ToArray();

        DirectoryInfo[] ClassFolders(string className)
        {
            if (className.IndexOfAny(['/', '\\', ':']) >= 0)
                throw new ArgumentException("Klasse muss ein einzelner Ordnername sein.");
            if (!classFolders.TryGetValue(className, out var directories))
            {
                var classPath = Resolve(root, className);
                directories = new DirectoryInfo(classPath).GetDirectories();
                classFolders.Add(className, directories);
            }
            return directories;
        }

        string[] FindFreeFolders(StudentRecord record)
        {
            try
            {
                return ClassFolders(record.Student.ClassName)
                    .Where(d => (d.Attributes & FileAttributes.ReparsePoint) == 0
                        && !assigned.Contains(d.FullName)
                        && !stored[d.FullName].Any(s => s.Student.ExternalStudentId != record.Student.ExternalStudentId))
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).Select(d => d.FullName).ToArray();
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                return [];
            }
        }

        DistributionTarget FindTarget(StudentRecord record)
        {
            try
            {
                var student = record.Student;
                var folder = student.LastName + "." + student.FirstName;
                var derivedPath = student.ClassName + "/" + folder;
                if (record.MappingConfirmed || string.IsNullOrWhiteSpace(student.LastName) || string.IsNullOrWhiteSpace(student.FirstName)
                    || !string.Equals(record.HomeDirectory.Replace('\\', '/'), derivedPath, StringComparison.OrdinalIgnoreCase))
                    return new(record, Path.TrimEndingDirectorySeparator(Resolve(root, record.HomeDirectory)), false, "");

                var firstNames = student.FirstName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                var candidates = ClassFolders(student.ClassName).Where(d => MatchesName(d.Name, folder, student.LastName, firstNames))
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToArray();
                if (candidates.Length == 0)
                    throw new DirectoryNotFoundException("Kein passender Schülerordner für " + derivedPath + ".");
                if (candidates.Length > 1)
                    return new(record, null, false, "Mehrere passende Ordner: " + string.Join(", ", candidates.Select(d => d.Name)));

                var path = Resolve(root, candidates[0].FullName);
                var isVariant = !string.Equals(candidates[0].Name, folder, StringComparison.OrdinalIgnoreCase);
                return new(record, path, isVariant, "");
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                return new(record, null, false, ex.Message);
            }
        }
    }

    private static bool MatchesName(string name, string exact, string lastName, string[] firstNames)
    {
        if (string.Equals(name, exact, StringComparison.OrdinalIgnoreCase)) return true;
        var prefix = lastName + ".";
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

        var givenNames = string.Concat(name[prefix.Length..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (givenNames.EndsWith('2')) givenNames = givenNames[..^1];
        if (string.Equals(givenNames, string.Concat(firstNames), StringComparison.OrdinalIgnoreCase)) return true;

        // The ASV export can contain only the Rufname. An attached name is a suggestion for individual review.
        var first = firstNames[0];
        return givenNames.StartsWith(first, StringComparison.OrdinalIgnoreCase) && givenNames[first.Length..].All(char.IsLetter);
    }

    public static string Resolve(string root, string imported)
    {
        if (!Path.IsPathFullyQualified(root) || string.IsNullOrWhiteSpace(imported))
            throw new ArgumentException("Laufwerk/Grundverzeichnis oder Schülerordner fehlt. Vorname/Nachname oder optionalen Ordner importieren.");
        root = Path.GetFullPath(root);
        var path = Path.IsPathFullyQualified(imported) ? Path.GetFullPath(imported) : Path.GetFullPath(Path.Combine(root, imported));
        var relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(p => p is ".." or ".") || relative == ".")
            throw new ArgumentException("Schülerordner liegt außerhalb des gewählten Grundverzeichnisses.");
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Schülerordner nicht gefunden: " + path);
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Verknüpfte Ordner bitte durch einen direkten Pfad ersetzen.");
            if (string.Equals(directory.FullName.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) break;
        }
        return path;
    }
}
