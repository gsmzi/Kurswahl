using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace GsmKurswahl.TeacherApp;

internal static class Program
{
    [STAThread] public static void Main(string[] args)
    {
        if (args is ["--prepare-demo-80", var root])
        {
            DemoWorkspace.Prepare(root, DemoCatalog());
            return;
        }
        if (args is ["--prepare-test-stage", var stage, var stageRoot])
        {
            Environment.ExitCode = PrepareStage(stage, stageRoot);
            return;
        }
        AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
    internal static GsmKurswahl.Domain.OfferCatalog DemoCatalog() =>
        GsmKurswahl.Storage.JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json")));
    // Runs without a window for the test package's batch files, which show this file if the exit code is not 0.
    private static int PrepareStage(string stage, string root)
    {
        var report = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + "-fehler.txt";
        // On a fresh computer the common test folder does not exist yet.
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        try
        {
            File.Delete(report);
            DemoStages.Prepare(stage, root, DemoCatalog());
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(report, "Teststand konnte nicht vorbereitet werden: " + ex.Message);
            return 1;
        }
    }
}
public sealed class App : Avalonia.Application
{
    private TeacherProgramStartup? startup;
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        var theme = new FluentTheme();
        theme.Palettes[ThemeVariant.Light] = new ColorPaletteResources { Accent = Color.Parse("#1E287D") };
        Styles.Add(theme);
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            startup = TeacherProgramStartup.ForArguments(desktop.Args ?? [], AppContext.BaseDirectory, out var args);
            desktop.Exit += (_, _) => startup.Dispose();
            desktop.MainWindow = startup.OpenMainWindow(() => CreateMainWindow(args));
        }
        base.OnFrameworkInitializationCompleted();
    }
    private static Window CreateMainWindow(string[] args)
    {
        if (args is ["--test-stage", var stage, var stageRoot])
        {
            try
            {
                DemoStages.Prepare(stage, stageRoot, Program.DemoCatalog());
                var window = new TeacherWindow(stageRoot);
                window.Title += " · TESTSTAND: " + DemoStages.Find(stage).Title;
                return window;
            }
            catch (Exception ex)
            {
                return new Window { Title = "Teststand", Width = 700, Height = 300,
                    Content = new TextBlock { Text = "Teststand konnte nicht geöffnet werden.\n\n" + ex.Message, Margin = new Avalonia.Thickness(24), TextWrapping = TextWrapping.Wrap } };
            }
        }
        if (args is ["--demo-80", var root])
        {
            try
            {
                DemoWorkspace.Prepare(root, Program.DemoCatalog());
                var window = new TeacherWindow(root);
                window.Title += " · DEMO mit 80 Schülern";
                return window;
            }
            catch (Exception ex)
            {
                return new Window { Title = "Demo-Testbestand", Width = 700, Height = 300,
                    Content = new TextBlock { Text = "Testbestand konnte nicht geöffnet werden.\n\n" + ex.Message, Margin = new Avalonia.Thickness(24), TextWrapping = TextWrapping.Wrap } };
            }
        }
        if (args is ["--paket-ordner", var packageRoot])
        {
            var full = Path.GetFullPath(packageRoot);
            return new TeacherWindow(Path.Combine(full, GsmKurswahl.AdministrationStorage.PedagogicalTransferPackage.WorkspaceFolderName), full);
        }
        return new TeacherWindow();
    }
}
