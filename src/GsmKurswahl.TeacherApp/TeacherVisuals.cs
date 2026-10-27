using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Markup.Xaml.Styling;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;

namespace GsmKurswahl.TeacherApp;

public sealed partial class TeacherWindow
{
    private static readonly IBrush Ink = Brush.Parse("#203448");
    private static readonly IBrush Muted = Brush.Parse("#526476");
    private static readonly IBrush Accent = Brush.Parse("#176B68");
    private static readonly IBrush SoftAccent = Brush.Parse("#E2F1EE");
    private static readonly IBrush Line = Brush.Parse("#DAE3E9");
    private readonly StackPanel workflowProgress = new() { Name = "WorkflowProgress", Spacing = 10 };
    private readonly TextBlock sectionLabel = new() { FontSize = 12, Foreground = Muted, FontWeight = FontWeight.SemiBold };

    private void BuildVisualShell()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Background = Brush.Parse("#F3F6F8"); Foreground = Ink; FontSize = 14;
        Styles.Add(new StyleInclude(new Uri("avares://GsmKurswahl.TeacherApp/")) { Source = new Uri("avares://GsmKurswahl.TeacherApp/TeacherStyles.axaml") });
        var root = new DockPanel { Margin = new Thickness(28, 20) };
        var header = new StackPanel { Spacing = 14 };
        var brand = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var titles = new StackPanel { Spacing = 4 };
        titles.Children.Add(new TextBlock { Text = "GSM  /  KURSWAHL · LEHRERVERWALTUNG", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Accent, LetterSpacing = 1.4 });
        titles.Children.Add(heading); brand.Children.Add(titles);
        var badge = new Border { Background = Brush.Parse("#FFF1D9"), CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 6), VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "Erprobungsbetrieb", Foreground = Brush.Parse("#785311"), FontSize = 12, FontWeight = FontWeight.SemiBold } };
        ToolTip.SetTip(badge, "Nur synthetische Daten. Fachliche Freigabe offen; keine verbindliche Abgabe.");
        Grid.SetColumn(badge, 1); brand.Children.Add(badge); header.Children.Add(brand);
        header.Children.Add(navigation);
        header.Children.Add(workflowProgress);
        header.Margin = new Thickness(0, 0, 0, 12);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var footer = new StackPanel { Spacing = 4, Margin = new Thickness(0, 12, 0, 0) };
        footer.Children.Add(new ScrollViewer { Content = status, MaxHeight = 54 });
        footer.Children.Add(new TextBlock { Text = "Synthetische Daten · fachliche Freigabe offen · keine verbindliche Abgabe", FontSize = 11, Foreground = Muted });
        status.FontSize = 12; status.Foreground = Muted;
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var content = new DockPanel();
        sectionLabel.Margin = new Thickness(8, 0, 8, 12); DockPanel.SetDock(sectionLabel, Dock.Top); content.Children.Add(sectionLabel); content.Children.Add(pageHost);
        root.Children.Add(Surface(content, 20)); Content = root;
    }
    private static Border Surface(Control content, double padding = 16) => new()
    {
        Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(padding), Child = content
    };
    private static Button Primary(Button button) { button.Classes.Add("primary"); return button; }
    private void UpdateVisualNavigation()
    {
        sectionLabel.Text = visiblePage switch
        {
            "offer" => "EINRICHTUNG  /  FACHANGEBOT", "import" => "SCHÜLERLISTE  /  IMPORT & ABGLEICH",
            "profiles" => "SCHÜLERDATEIEN  /  VERTEILUNG", "collection" => "KURSWAHLEN  /  EINGANG & KONFLIKTE",
            "people" => "SCHÜLERBESTAND", "settings" => "EINSTELLUNGEN", "help" => "HILFE & DATEIEN",
            "manage" => "JAHRGANG VERWALTEN", "catalog" => "FACHANGEBOT", "new" => "WILLKOMMEN", _ => "JAHRGANGSÜBERSICHT"
        };
        foreach (var button in navigation.Children.OfType<Button>())
        {
            button.Classes.Add("nav");
            var selected = button.Content?.ToString() switch
            {
                "Übersicht" => visiblePage == "start", "Schülerbestand" => visiblePage == "people",
                "Jahrgang verwalten" => visiblePage is "manage" or "catalog", "Einstellungen" => visiblePage == "settings",
                "Hilfe" => visiblePage == "help", "Zur Einrichtung" => visiblePage is "new" or "offer" or "import" or "profiles", _ => false
            };
            button.Classes.Set("selected", selected);
        }
    }
    private void UpdateWorkflowProgress()
    {
        workflowProgress.Children.Clear();
        var active = workspace?.Active;
        if (active is null && visiblePage != "new") return;
        var summary = active is not null && store is not null && session is not null ? TeacherDashboardBuilder.Build(store, session) : null;
        var setup = active?.Step;
        var stage = setup switch { YearSetupStep.Offer => 0, YearSetupStep.Import => 1, YearSetupStep.Mapping or YearSetupStep.Review => 2, _ => -1 };
        var distributed = summary is { Students: > 0 } && summary.Delivered == summary.Students;
        string[] titles = ["Fächer auswählen", "Schülerliste importieren", "Dateien verteilen"];
        string[] descriptions = ["Leistungsfachangebot festlegen", "Liste gemeinsam übernehmen", "Laufwerk wählen und zustellen"];
        var strip = new Grid { ColumnDefinitions = new ColumnDefinitions("*,28,*,28,*") };
        for (var i = 0; i < 3; i++)
        {
            var stepIndex = i;
            var complete = active is not null && (i == 0 ? setup != YearSetupStep.Offer : i == 1 ? stage > 1 || setup == YearSetupStep.Complete && summary?.Students > 0 : distributed);
            var current = stage == i || setup == YearSetupStep.Complete && i == 2 && !distributed;
            var state = current ? "Aktueller Schritt" : complete ? "Erledigt" : "Noch offen";
            if (setup == YearSetupStep.Complete && i == 2 && !distributed) state = "Zustellungen offen";
            var caption = new StackPanel { Spacing = 3 };
            caption.Children.Add(new TextBlock { Text = titles[i], FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = current ? Accent : Ink });
            caption.Children.Add(new TextBlock { Name = "WorkflowState" + i, Text = state, FontSize = 12, Foreground = current || complete ? Accent : Muted });
            var number = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Background = current ? Accent : complete ? SoftAccent : Brush.Parse("#EDF1F4"), VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = complete && !current ? "✓" : (i + 1).ToString(), Foreground = current ? Brushes.White : complete ? Accent : Muted, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("42,*") }; row.Children.Add(number); Grid.SetColumn(caption, 1); row.Children.Add(caption);
            var tile = Surface(row, 12); tile.Name = "WorkflowStep" + i;
            tile.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            tile.Background = current ? Brush.Parse("#F0F8F6") : Brushes.White; tile.BorderBrush = current ? Accent : Line;
            tile.PointerPressed += (_, _) => JumpToWorkflowStep(stepIndex);
            AutomationProperties.SetName(tile, $"Schritt {i + 1}: {titles[i]}. {state}.");
            ToolTip.SetTip(tile, descriptions[i] + ". Anklicken, um direkt zu diesem Schritt zu wechseln. Die Anzeige beschreibt den Arbeitsablauf, keine fachliche Freigabe.");
            Grid.SetColumn(tile, i * 2); strip.Children.Add(tile);
            if (i < 2)
            {
                var connector = new Border { Height = 2, Background = complete ? Accent : Line, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(connector, i * 2 + 1); strip.Children.Add(connector);
            }
        }
        workflowProgress.Children.Add(strip);
    }
    private static Control SummaryCards(TeacherDashboard summary)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
        (string Value, string Label)[] values = [(summary.Students.ToString(), "Schüler im Jahrgang"), ($"{summary.Delivered} / {summary.Students}", "Profile zugestellt"), (summary.Choices.ToString(), "Wahlen eingelesen"), (summary.Reviews.ToString(), "Fachlich zu klären")];
        for (var i = 0; i < values.Length; i++)
        {
            var body = new StackPanel { Spacing = 5 };
            body.Children.Add(new TextBlock { Text = values[i].Value, FontSize = 28, FontWeight = FontWeight.SemiBold, Foreground = i == 3 && summary.Reviews > 0 ? Brush.Parse("#946312") : Accent });
            body.Children.Add(new TextBlock { Text = values[i].Label, FontSize = 12, Foreground = Muted, TextWrapping = TextWrapping.Wrap });
            var card = Surface(body); card.Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0); Grid.SetColumn(card, i); grid.Children.Add(card);
        }
        return grid;
    }
}
