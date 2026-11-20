namespace GsmKurswahl.StudentApp.Tests;

/// <summary>
/// Skip reason for headless UI facts that only apply on Windows (AvaloniaFactAttribute is sealed, so the
/// reason is chosen at build time via WINDOWS_HOST from the test project). The teacher start lock
/// deliberately recognizes a foreign lock only by the Windows sharing-violation code, because both
/// programs ship for win-x64 only. Elsewhere the facts are reported as skipped, never as passed.
/// Usage: [AvaloniaFact(Skip = WindowsOnly.Skip)]
/// </summary>
internal static class WindowsOnly
{
#if WINDOWS_HOST
    public const string? Skip = null;
#else
    public const string? Skip = "Die Startsperre erkennt eine fremde Sperre nur unter Windows (Zielplattform win-x64).";
#endif
}
