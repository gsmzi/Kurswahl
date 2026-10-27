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
                var profile = new SignedProfileReader().Read(Path.GetDirectoryName(path)!, settings.DemoStudentId, catalog.GraduationYear);
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
