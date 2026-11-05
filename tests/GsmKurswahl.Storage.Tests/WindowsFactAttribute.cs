namespace GsmKurswahl.Storage.Tests;

/// <summary>Fact that runs only on Windows, e.g. for GSM presets with drive-letter paths; reported as skipped elsewhere.</summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Windows-Pfade (Laufwerksbuchstaben) gelten nur unter Windows.";
    }
}
