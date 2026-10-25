using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Themes.Fluent;

namespace GsmKurswahl.TeacherApp;

internal static class Program
{
    [STAThread] public static void Main(string[] args) => AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
}
public sealed class App : Avalonia.Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new Window
            {
                Title = "GSM-Kurswahl | Lehrerprogramm · M1 Grundgerüst", Width = 800, Height = 420,
                Content = new TextBlock { Margin = new Thickness(32), FontSize = 20, TextWrapping = TextWrapping.Wrap,
                    Text = "GSM-Kurswahl · Lehrerverwaltung\n\nM1: getrenntes Programmgrundgerüst.\n\nM3: Stammdatenimport, signierte Einzelprofile, geschützter Verwaltungsbestand und Wahlhistorie.\nM4: Kursplanung und Szenarien.\nM5: ASV-Abgleich und Exporte.\n\nNoch kein administrativer Datenzugriff und keine Freigabe von Kurswahlen." }
            };
        base.OnFrameworkInitializationCompleted();
    }
}
