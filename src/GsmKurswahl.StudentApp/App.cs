using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using GsmKurswahl.Application;
using GsmKurswahl.Storage;

namespace GsmKurswahl.StudentApp;

public sealed class App : Avalonia.Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                var configuredSettings = Environment.GetEnvironmentVariable("GSM_KURSWAHL_SETTINGS");
                var settingsPath = string.IsNullOrWhiteSpace(configuredSettings)
                    ? Path.Combine(AppContext.BaseDirectory, "appsettings.json")
                    : configuredSettings;
                var settings = StudentSettings.Load(settingsPath);
                var catalog = JsonDocuments.ReadCatalog(File.ReadAllBytes(settings.ResolveCatalogPath(settingsPath)));
                var path = settings.ResolveChoicePath();
                // Only the protected installation supplies trust. Never load keys from the student home or settings override.
                var trustPath = Path.Combine(AppContext.BaseDirectory, "profile-trust.json");
                GsmKurswahl.Domain.ProfileContext profile;
                try
                {
                    var trust = File.Exists(trustPath)
                        ? System.Text.Json.JsonSerializer.Deserialize<GsmKurswahl.Domain.ProfileTrust>(File.ReadAllBytes(trustPath), JsonDocuments.Options)
                        : null;
                    profile = new SignedProfileReader(trust ?? new(1, [], []), new OperatingSystemIdentityProvider())
                        .Read(Path.GetDirectoryName(path)!, settings.DemoStudentId, catalog.GraduationYear);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
                {
                    profile = new(GsmKurswahl.Domain.ProfileStatus.ProfileInvalid, GsmKurswahl.Domain.PriorEducation.Unknown)
                    { Detail = "Vertrauenskonfiguration nicht lesbar. Oberstufenberatung kontaktieren; Entwurf bleibt erhalten." };
                }
                var session = new StudentSession(catalog, settings.DemoStudentId, new JsonChoiceStore(path, settings.ResolveHomeDirectory()), profile);
                desktop.MainWindow = new MainWindow(session, path, new OperatingSystemIdentityProvider().GetCurrent().Username);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
            {
                desktop.MainWindow = new Window
                {
                    Title = "GSM-Kurswahl – Konfiguration prüfen", Width = 700, Height = 300,
                    Content = new TextBlock { Margin = new Thickness(24), TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Text = "Die Anwendungskonfiguration konnte nicht geladen werden. Es wurden keine Wahldateien verändert.\n\n" + ex.Message }
                };
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
