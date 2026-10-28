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
            DemoWorkspace.Prepare(root, GsmKurswahl.Storage.JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json"))));
            return;
        }
        AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}
public sealed class App : Avalonia.Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.Args is ["--demo-80", var root])
            {
                try
                {
                    DemoWorkspace.Prepare(root, GsmKurswahl.Storage.JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json"))));
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
