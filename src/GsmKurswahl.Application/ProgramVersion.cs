using System.Reflection;

namespace GsmKurswahl.Application;

/// <summary>Version of the running program as set by build/Version.targets (version.txt plus commit count).</summary>
public static class ProgramVersion
{
    /// <summary>Full form, e.g. "1.0.3+1a2b3c4d" or "1.0.3+1a2b3c4d.geaendert" for a build with uncommitted changes.</summary>
    public static string Full { get; } = Read(Assembly.GetEntryAssembly() ?? typeof(ProgramVersion).Assembly);

    /// <summary>Short form for window titles, e.g. "1.0.3".</summary>
    public static string Short => Full.Split('+')[0];

    public static string Read(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? assembly.GetName().Version?.ToString(3) ?? "unbekannt";
}
