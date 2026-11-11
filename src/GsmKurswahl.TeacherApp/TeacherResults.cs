using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GsmKurswahl.Domain;

namespace GsmKurswahl.TeacherApp;

public sealed partial class TeacherWindow
{
    private void ShowElection()
    {
        Ready(); visiblePage = "wahl";
        var panel = new StackPanel { Spacing = 12 };
        Intro(panel, "Wahl vorbereiten und einsammeln", "Der Ablauf oben führt von der Fachauswahl bis zum Wahleingang. Voraussetzungen und persönliche Dateien werden hier verwaltet.");
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var button in new[] { Button("Fachauswahl", () => JumpToWorkflowStep(0)), Button("Schülerliste importieren", () => ShowPage("import")),
            Button("Schülerbestand", () => ShowPage("people")), Button("Schülerdateien verteilen", () => ShowPage("profiles")),
            Button("Kurswahlen einsammeln", () => ShowPage("collection")) })
        { button.Margin = new Thickness(0, 0, 8, 8); actions.Children.Add(button); }
        panel.Children.Add(actions);
        panel.Children.Add(Text("Nach dem Einsammeln läuft alles im Lehrerprogramm: unter „Kurswahlen“ prüfen, ändern und drucken, unter „Kursplanung“ Wahlzahlen ansehen und Kurse streichen."));
        pageHost.Content = Scroll(panel); UpdateVisualNavigation(); UpdateWorkflowProgress();
    }

    private static Grid CompactLine(string columns, Control[] cells)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), MinHeight = 27, Margin = new Thickness(6, 1) };
        for (var i = 0; i < cells.Length; i++)
        {
            cells[i].Margin = new Thickness(4, 2); cells[i].VerticalAlignment = VerticalAlignment.Center;
            if (cells[i] is TextBlock text) text.FontSize = 13;
            Grid.SetColumn(cells[i], i); grid.Children.Add(cells[i]);
        }
        return grid;
    }
}
