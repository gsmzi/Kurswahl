namespace GsmKurswahl.ImportExport;

/// <summary>Access to embedded GSM stationery assets shared across print and UI layers.</summary>
public static class SchoolAssets
{
    public const string LogoResourceName = "GsmKurswahl.ImportExport.Assets.SchoolLogo.jpeg";
    public const int LogoPixelWidth = 2315;
    public const int LogoPixelHeight = 816;
    public static double LogoAspectRatio => (double)LogoPixelWidth / LogoPixelHeight;

    public static Stream OpenLogoStream() =>
        typeof(SchoolAssets).Assembly.GetManifestResourceStream(LogoResourceName)
        ?? throw new InvalidOperationException("Eingebettetes Schullogo fehlt.");
}
