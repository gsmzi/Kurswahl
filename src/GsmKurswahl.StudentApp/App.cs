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
                var arguments = desktop.Args ?? [];
                var folderIndex = Array.IndexOf(arguments, "--wahlordner");
                var personalFolder = folderIndex >= 0 && folderIndex + 1 < arguments.Length
                    ? arguments[folderIndex + 1] : Environment.GetEnvironmentVariable("GSM_KURSWAHL_HOME");
                if (!string.IsNullOrWhiteSpace(personalFolder))
                    settings = settings with { HomeDirectory = personalFolder, ChoiceDirectory = "" };
                var catalog = JsonDocuments.ReadCatalog(File.ReadAllBytes(settings.ResolveCatalogPath(settingsPath)));
                var path = settings.ResolveChoicePath();
                var personal = settings.ReadPersonalProfile(catalog.GraduationYear);
                var session = new StudentSession(catalog, personal.StudentId, new JsonChoiceStore(path, settings.ResolveHomeDirectory()), personal.Profile);
                desktop.MainWindow = new MainWindow(session, path, new OperatingSystemIdentityProvider().GetCurrent().Username);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
            {
                desktop.MainWindow = new Window
                {
                    Title = "GSM-Kurswahl – Konfiguration prüfen", Width = 700, Height = 300,
                    Content = new TextBlock { Margin = new Thickness(24), TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Text = "Der persönliche Wahlordner konnte nicht geladen werden. Starten Sie mit --wahlordner und dem von der Schule zugeordneten Ordner oder setzen Sie GSM_KURSWAHL_HOME. Dort müssen profil.json und fachangebot.json liegen. Es wurden keine Wahldateien verändert.\n\n" + ex.Message }
                };
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
