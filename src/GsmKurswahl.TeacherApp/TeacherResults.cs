using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;

namespace GsmKurswahl.TeacherApp;

public sealed partial class TeacherWindow
{
    private void ShowElection()
    {
        Ready(); ClearError(); visiblePage = "wahl";
        var summary = TeacherDashboardBuilder.Build(store!, session!);
        var panel = new StackPanel { Spacing = 14 };
        Intro(panel, "Wahl vorbereiten und einsammeln", "Der Ablauf oben führt von der Fachauswahl (Verwaltungsnetz) über das Verteilen und Einsammeln im pädagogischen Netz bis zur Auswertung zurück im Verwaltungsnetz.");

        var workflowCard = new StackPanel { Spacing = 10 };
        workflowCard.Children.Add(Heading("Ablaufschritte"));
        workflowCard.Children.Add(new TextBlock
        {
            Text = $"{summary.Students} Personen im Jahrgang · {summary.Delivered} von {summary.Students} Profilen zugestellt · {summary.Choices} von {summary.Students} Wahlen eingelesen",
            Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 13
        });
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var button in new[] {
            Button("Fachauswahl", () => JumpToWorkflowStep(0), "Leistungsfächer und optionale Kurse einsehen oder vor Wahleingang im Verwaltungsnetz bearbeiten."),
            Button("Schülerliste importieren", () => ShowPage("import"), "Klassen-10-Liste (XLSX/CSV) mit Namen und Vorbildung im Verwaltungsnetz übernehmen."),
            Button("Schülerdateien verteilen", () => ShowPage("profiles"), "Muss im pädagogischen Netz erfolgen: dort Paket inkl. Programm entpacken, persönliche Profile und Fachangebot in die Wahlordner schreiben."),
            Button("Kurswahlen einsammeln", () => ShowPage("collection"), "Im pädagogischen Netz Wahleingang prüfen, Konflikte klären, nach Abgabetermin räumen und Transportdatei zurück ins Verwaltungsnetz speichern.") })
        { button.Margin = new Thickness(0, 0, 8, 8); actions.Children.Add(button); }
        workflowCard.Children.Add(actions);
        panel.Children.Add(Surface(workflowCard, 14));

        var peopleCard = new StackPanel { Spacing = 8 };
        peopleCard.Children.Add(Heading("Einzelne Personen & Sonderfälle"));
        peopleCard.Children.Add(new TextBlock { Text = "Vorbildung, Wahlordner, Nachwahlen, Einzeländerungen und komplette Änderungshistorie pro Person.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
        peopleCard.Children.Add(Button("Schülerbestand", () => ShowPage("people"), "Einzelne Personen öffnen und Stammdaten, Vorbildung oder Historie bearbeiten."));
        panel.Children.Add(SubtleCard(peopleCard, 14));

        panel.Children.Add(new TextBlock { Text = "Nach dem Einsammeln läuft alles im Lehrerprogramm: unter „Kurswahlen“ prüfen, ändern und drucken, unter „Kursplanung“ Wahlzahlen ansehen und Kurse streichen.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
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
