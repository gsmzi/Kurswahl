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
        panel.Children.Add(Text("Nach dem Einsammeln: Wahlergebnisse prüfen, Abgänger markieren und unter Kursplanung die verbleibenden Wahlzahlen vergleichen."));
        pageHost.Content = Scroll(panel); UpdateVisualNavigation(); UpdateWorkflowProgress();
    }

    private void ShowResults()
    {
        Ready(); visiblePage = "results";
        var root = new DockPanel();
        var top = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 8) };
        var search = new TextBox { Name = "ResultsSearch", Watermark = "Name, Klasse oder Schüler-ID filtern", Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
        var resultFilter = new ComboBox { Name = "ResultsStatusFilter", SelectedIndex = 0, MinWidth = 210, Margin = new Thickness(10, 0, 0, 0),
            ItemsSource = new[] { "Alle Prüfstände", "Regelverstoß", "Fachlich zu klären", "Formal gültig", "Nachwahl offen", "Feststellungsprüfung nötig" } };
        var filters = new StackPanel { Orientation = Orientation.Horizontal }; filters.Children.Add(search); filters.Children.Add(resultFilter);
        var summary = Text(""); top.Children.Add(summary); top.Children.Add(filters);
        const string columns = "175,50,130,*,95,150,135";
        top.Children.Add(CompactLine(columns, [Text("Schüler"), Text("Klasse"), Text("Leistungsfächer"), Text("Basis- und Wahlfächer"), Text("Mündlich"), Text("Prüfstand"), Text("Abgängerstatus")]));
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var rows = new StackPanel { Spacing = 1, Name = "ResultRows" };
        root.Children.Add(new ScrollViewer { Content = rows });
        void Render()
        {
            rows.Children.Clear();
            var states = session!.Overview().Where(s => s.Record.Student.GraduationYear == session.Catalog.GraduationYear).ToArray();
            var withChoice = states.Where(s => s.Active is not null).ToArray();
            summary.Text = $"{withChoice.Length} Kurswahlen · {states.Length - withChoice.Length} noch ohne Wahl · {withChoice.Count(s => s.Validation?.Status == ReviewStatus.Invalid)} mit Regelverstoß · {withChoice.Count(s => s.Validation?.Status == ReviewStatus.NeedsReview)} fachlich zu klären · {withChoice.Count(s => s.Validation?.Status == ReviewStatus.Valid)} formal gültig. Mauszeiger auf den Prüfstand zeigt die Hinweise.";
            var index = 0;
            foreach (var state in states.Where(s => s.Active is not null &&
                (s.Record.Student.SyntheticDisplayName + " " + s.Record.Student.ClassName + " " + s.Record.Student.ExternalStudentId).Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)))
            {
                var religion = GsmKurswahl.Application.ReligionCases.For(state);
                if (resultFilter.SelectedIndex switch
                    {
                        1 => state.Validation?.Status != ReviewStatus.Invalid, 2 => state.Validation?.Status != ReviewStatus.NeedsReview,
                        3 => state.Validation?.Status != ReviewStatus.Valid, 4 => !state.RequiresReselection, 5 => religion?.AssessmentSubject is null, _ => false
                    }) continue;
                var record = state.Record;
                var choice = state.Active!.Snapshot;
                string Selection(CourseSelection s) => s.SubjectId + (s.Variant == CourseVariant.Bilingual ? " bili" : "")
                    + (s.Type == CourseType.Bf && s.SubjectId is "GK" or "GEO" ? " HJ " + string.Join(",", s.Semesters)
                        : s.Semesters.SequenceEqual(new[] { 1, 2, 3, 4 }) ? "" : s.Semesters.SequenceEqual(new[] { 1, 2 }) ? " KS1" : s.Semesters.SequenceEqual(new[] { 3, 4 }) ? " KS2" : " HJ " + string.Join(',', s.Semesters));
                var lf = string.Join(" · ", choice.Selections.Where(s => s.Type == CourseType.Lf).Select(Selection));
                var bf = string.Join(", ", choice.Selections.Where(s => s.Type != CourseType.Lf).Select(Selection));
                var student = Text(record.Student.SyntheticDisplayName);
                ToolTip.SetTip(student, record.Student.ExternalStudentId);
                var review = Text(FormatStatus(state.Validation?.Status) + (state.RequiresReselection ? " · Nachwahl" : "") + (religion?.AssessmentSubject is { } fp ? " · FP " + fp : ""));
                review.Name = "ResultReview_" + record.Student.ExternalStudentId;
                review.Foreground = state.Validation?.Status switch { ReviewStatus.Invalid => Brush.Parse("#A32D2D"), ReviewStatus.NeedsReview => Brush.Parse("#854F0B"), ReviewStatus.Valid => Brush.Parse("#3B6D11"), _ => Muted };
                var hints = state.Validation?.Messages.Select(m => (m.Rule.Severity == ReviewStatus.Invalid ? "Regelverstoß: " : "Zu klären: ") + m.Explanation).ToArray() ?? [];
                ToolTip.SetTip(review, new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 520,
                    Text = hints.Length == 0 ? "Keine offenen Punkte." : string.Join("\n\n", hints) + (religion is null ? "" : "\n\n" + religion.Note) });
                var oral = Text(string.Join(", ", choice.PlannedOralSubjects));
                var departure = new ComboBox { Name = "ResultDeparture_" + record.Student.ExternalStudentId,
                    ItemsSource = new[] { "Aktiv", "Voraussichtlich", "Bestätigt" }, SelectedIndex = (int)record.Student.Departure,
                    MinHeight = 28, Padding = new Thickness(6, 2), HorizontalAlignment = HorizontalAlignment.Stretch };
                var updating = false;
                var line = CompactLine(columns, [student, Text(record.Student.ClassName), Text(lf), Text(bf), oral, review, departure]);
                void UpdateLineTip() => ToolTip.SetTip(line, "Mündliche Planung: " + string.Join(", ", choice.PlannedOralSubjects) + " · Abgängerstatus: " + record.Student.Departure + " · " + (state.RequiresReselection ? "Nachwahl / Klärung offen" : "Wahl erfasst") + " · Prüfstand: " + state.Validation?.Status);
                departure.SelectionChanged += (_, _) =>
                {
                    if (updating || departure.SelectedIndex < 0 || departure.SelectedIndex == (int)record.Student.Departure) return;
                    try
                    {
                        var next = record with { Student = record.Student with { Departure = (DepartureStatus)departure.SelectedIndex },
                            Version = record.Version + 1, ChangedAt = DateTimeOffset.UtcNow, Actor = Actor };
                        store!.SaveStudent(next, record.Version); record = next;
                        UpdateLineTip(); Refresh(preserveEditor: true);
                        status.Text = record.Student.SyntheticDisplayName + ": Abgängerstatus gespeichert. Kursplanung berücksichtigt die Änderung.";
                    }
                    catch (Exception ex)
                    {
                        updating = true; departure.SelectedIndex = (int)record.Student.Departure; updating = false;
                        status.Text = "Abgängerstatus nicht gespeichert: " + ex.Message + " Wahlergebnisse erneut öffnen.";
                    }
                };
                UpdateLineTip();
                rows.Children.Add(new Border { Background = index++ % 2 == 0 ? Brush.Parse("#F1F5F7") : Brushes.White, Child = line });
            }
        }
        search.TextChanged += (_, _) => Render(); resultFilter.SelectionChanged += (_, _) => Render(); Render();
        pageHost.Content = root; UpdateVisualNavigation();
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
