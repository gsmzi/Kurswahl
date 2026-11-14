using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GsmKurswahl.AdministrationStorage;

namespace GsmKurswahl.TeacherApp;

/// <summary>Acquire the installation lock before creating any administrative window or opening data.</summary>
public sealed class TeacherProgramStartup(string programDirectory) : IDisposable
{
    private TeacherProgramLease? lease;

    // The package launcher runs a local program copy but locks the original shared installation.
    public static TeacherProgramStartup ForArguments(string[] arguments, string defaultDirectory, out string[] windowArguments)
    {
        if (arguments is [.., "--startsperre", var sharedDirectory])
        {
            windowArguments = arguments[..^2];
            return new TeacherProgramStartup(sharedDirectory);
        }
        windowArguments = arguments;
        return new TeacherProgramStartup(defaultDirectory);
    }

    public Window OpenMainWindow(Func<Window> openAdministration)
    {
        try { lease = new TeacherProgramLease(programDirectory); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var explanation = ex is TeacherProgramInUseException ? ex.Message
                : "Die Startsperre konnte nicht geöffnet werden. Bitte prüfen Sie, ob der Programmordner erreichbar ist, "
                    + "Leserechte vorhanden sind und die mitgelieferte Datei „" + TeacherProgramLease.FileName + "“ im Programmordner liegt. "
                    + "Das Lehrerprogramm bleibt geschlossen. Wenden Sie sich gegebenenfalls an die Betreuung.";
            var close = new Button { Content = "Schließen", HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true, IsCancel = true };
            var window = new Window
            {
                Title = "GSM-Kurswahl | Start nicht möglich", Width = 620, Height = 310, CanResize = false,
                Content = new StackPanel
                {
                    Margin = new Thickness(24), Spacing = 20,
                    Children =
                    {
                        new TextBlock { Text = "Lehrerprogramm kann nicht gestartet werden", FontSize = 22, TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Name = "StartupLockMessage", Text = explanation, TextWrapping = TextWrapping.Wrap },
                        close
                    }
                }
            };
            close.Click += (_, _) => window.Close();
            return window;
        }
        try { return openAdministration(); }
        catch { Dispose(); throw; }
    }

    public void Dispose() { lease?.Dispose(); lease = null; }
}
