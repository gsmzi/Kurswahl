using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
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
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.Args is ["--test-stage", var stage, var stageRoot])
            {
                try
                {
                    DemoStages.Prepare(stage, stageRoot, Program.DemoCatalog());
                    desktop.MainWindow = new TeacherWindow(stageRoot);
                    desktop.MainWindow.Title += " · TESTSTAND: " + DemoStages.Find(stage).Title;
                }
                catch (Exception ex)
                {
                    desktop.MainWindow = new Window { Title = "Teststand", Width = 700, Height = 300,
                        Content = new TextBlock { Text = "Teststand konnte nicht geöffnet werden.\n\n" + ex.Message, Margin = new Avalonia.Thickness(24), TextWrapping = TextWrapping.Wrap } };
                }
            }
            else if (desktop.Args is ["--demo-80", var root])
            {
                try
                {
                    DemoWorkspace.Prepare(root, Program.DemoCatalog());
                    desktop.MainWindow = new TeacherWindow(root);
                    desktop.MainWindow.Title += " · DEMO mit 80 Schülern";
                }
                catch (Exception ex)
                {
                    desktop.MainWindow = new Window { Title = "Demo-Testbestand", Width = 700, Height = 300,
                        Content = new TextBlock { Text = "Testbestand konnte nicht geöffnet werden.\n\n" + ex.Message, Margin = new Avalonia.Thickness(24), TextWrapping = TextWrapping.Wrap } };
                }
            }
            else desktop.MainWindow = new TeacherWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
