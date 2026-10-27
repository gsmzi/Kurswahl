using GsmKurswahl.Domain;

namespace GsmKurswahl.AdministrationStorage;

public static class StudentDirectories
{
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
