using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;

namespace GsmKurswahl.StudentApp;

public sealed class MainWindow : Window
{
    private readonly StudentSession session;
    private readonly TextBlock fileStatus = Text("", 15);
    private readonly TextBlock validation = Text("", 15);
    private readonly TextBlock summary = Text("", 16);
    private readonly TextBlock error = Text("", 15);
    private readonly StackPanel offerRows = new() { Spacing = 8 };
    private readonly StackPanel oralRows = new() { Spacing = 8 };
    private readonly TextBlock stepTitle = Text("", 20, FontWeight.Bold);
    private readonly TextBlock stepInfo = Text("", 14);
    private readonly Button previous = new() { Content = "_Zurück", Name = "PreviousStep" };
    private readonly Button next = new() { Content = "_Weiter", Name = "NextStep" };
    private readonly Dictionary<string, Control> rows = [];
    private readonly Button save = new() { Content = "Entwurf _speichern", Name = "SaveButton" };
    private readonly Button load = new() { Content = "Entwurf _laden", Name = "LoadButton" };
    private readonly CheckBox autosave = new() { Content = "Automatisch zwischenspeichern", IsChecked = true };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private readonly Dictionary<string, (CheckBox Selected, CheckBox[] Semesters)> boxes = [];
    private bool refreshing;
    private bool closeConfirmed;
    private bool closePromptOpen;

    public MainWindow(StudentSession session, string filePath, string account)
    {
        this.session = session;
        Title = "GSM-Kurswahl | Schülerprogramm · M1 Demo";
        Width = 1160; Height = 850; MinWidth = 860; MinHeight = 600;
        Background = new SolidColorBrush(Color.Parse("#F4F6FA"));
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), Margin = new Thickness(24) };
        var header = new StackPanel { Spacing = 5, Margin = new Thickness(0, 0, 0, 16) };
        header.Children.Add(Text("GSM-Kurswahl", 30, FontWeight.Bold));
        header.Children.Add(Text($"Schülerprogramm · synthetische Person {session.Choice.StudentId} · Abitur {session.Catalog.GraduationYear}", 17));
        header.Children.Add(Text("VORLÄUFIG · needs_review · Kein signiertes Profil · Keine verbindliche Abgabe in M1", 16, FontWeight.Bold));
        header.Children.Add(Text("Demoangebot: Schulische Freigabe und vollständige Rechtsprüfung stehen aus. Eingeschränkte Fächer erfordern ein geprüftes Profil.", 14));
        header.Children.Add(Text($"Betriebssystemkonto: {account} (keine bestätigte Schulzuordnung)\nDatei: {filePath}", 12));
        root.Children.Add(header);

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 0, 0, 14) };
        toolbar.Children.Add(save); toolbar.Children.Add(load); toolbar.Children.Add(autosave);
        Grid.SetRow(toolbar, 1); root.Children.Add(toolbar);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*") };
        var left = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 20, 0) };
        left.Children.Add(stepTitle);
        left.Children.Add(Text("1 Leistungsfächer → 2 Mündliche Fächer → 3 Pflichtbasisfächer → 4 Weitere Wahlen", 13));
        left.Children.Add(stepInfo);
        var navigation = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        navigation.Children.Add(previous); navigation.Children.Add(next);
        left.Children.Add(navigation);
        left.Children.Add(oralRows);
        left.Children.Add(Text("LF = Leistungsfach · BF = Basisfach · WF = Wahlfach · SK = Seminarkurs\nEin Fach nur in einer Kursart/Variante. LF immer HJ 1–4.", 13));
        left.Children.Add(offerRows);
        body.Children.Add(new ScrollViewer { Content = left });
        var right = new StackPanel { Spacing = 14 };
        right.Children.Add(Text("Belegung im Überblick", 20, FontWeight.Bold));
        right.Children.Add(summary);
        right.Children.Add(Text("Belegungszahlen und Wochenstunden sind keine Anrechnungs- oder Zulassungsbestätigung.", 14));
        right.Children.Add(validation);
        var rightScroll = new ScrollViewer { Content = right };
        Grid.SetColumn(rightScroll, 1); body.Children.Add(rightScroll);
        Grid.SetRow(body, 2); root.Children.Add(body);
        var footer = new StackPanel { Spacing = 5, Margin = new Thickness(0, 14, 0, 0) };
        error.Foreground = Brushes.DarkRed;
        footer.Children.Add(fileStatus); footer.Children.Add(error);
        Grid.SetRow(footer, 3); root.Children.Add(footer);
        Content = root;

        foreach (var offer in session.Catalog.Offers)
        {
            var availability = session.Availability(offer);
            var label = session.Catalog.Subjects.Single(s => s.Id == offer.SubjectId).Name;
            var type = offer.Type switch { CourseType.Lf => "LF", CourseType.Bf => "BF", CourseType.Elective => "WF", _ => "SK" };
            var selected = new CheckBox { Content = $"{label} · {type}" + (offer.Variant == CourseVariant.Bilingual ? " · bilingual" : ""), Name = "Offer_" + offer.Id.Replace('-', '_') };
            var row = new StackPanel { Spacing = 2 };
            row.Children.Add(selected);
            var semesterPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(24, 0, 0, 0) };
            var semesterBoxes = offer.Schedule.Select(h => new CheckBox { Content = $"HJ {h.Semester} ({h.WeeklyHours}h)", Tag = h.Semester, FontSize = 12 }).ToArray();
            foreach (var box in semesterBoxes)
            {
                semesterPanel.Children.Add(box);
                box.IsCheckedChanged += (_, _) =>
                {
                    if (!refreshing) Edit(() => session.SetSelection(offer.Id, semesterBoxes.Where(b => b.IsChecked == true).Select(b => (int)b.Tag!)));
                };
            }
            row.Children.Add(semesterPanel);
            if (!availability.CanSelect) row.Children.Add(Text(availability.Explanation, 12));
            var container = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(6), Padding = new Thickness(10), Child = row };
            offerRows.Children.Add(container);
            rows.Add(offer.Id, container);
            boxes.Add(offer.Id, (selected, semesterBoxes));
            selected.IsCheckedChanged += (_, _) =>
            {
                if (!refreshing) Edit(() => session.SetSelection(offer.Id, selected.IsChecked == true ? offer.Schedule.Select(h => h.Semester) : []));
            };
        }
        previous.Click += (_, _) => Edit(session.PreviousStep);
        next.Click += (_, _) => Edit(session.NextStep);
        save.Click += async (_, _) => await RunFileAction(() => session.SaveAsync());
        load.Click += async (_, _) =>
        {
            timer.Stop();
            if (!session.IsDirty || await Confirm("Ungespeicherte Änderungen verwerfen und die gespeicherte Wahl laden?"))
                await RunFileAction(() => session.LoadAsync(discardChanges: true));
        };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            if (autosave.IsChecked == true && session.IsDirty && !session.LoadFailed)
                await RunFileAction(() => session.SaveAsync());
        };
        session.Changed += Refresh;
        Opened += async (_, _) => await RunFileAction(() => session.LoadAsync());
        Closing += async (_, e) =>
        {
            if (closeConfirmed) return;
            if (session.IsBusy) { e.Cancel = true; return; }
            if (!session.IsDirty) return;
            e.Cancel = true; timer.Stop();
            if (closePromptOpen) return;
            closePromptOpen = true;
            try { if (await Confirm("Ungespeicherte Änderungen verwerfen und schließen?")) { closeConfirmed = true; Close(); } }
            finally { closePromptOpen = false; }
        };
        Closed += (_, _) => { timer.Stop(); session.Changed -= Refresh; };
        Refresh();
    }

    private void Edit(Action edit)
    {
        try
        {
            error.Text = ""; edit(); timer.Stop();
            if (autosave.IsChecked == true && !session.LoadFailed) timer.Start();
        }
        catch (InvalidOperationException ex) { error.Text = ex.Message; Refresh(); }
    }

    private async Task RunFileAction(Func<Task> action)
    {
        try { error.Text = ""; await action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        { error.Text = ex.Message + " Bei Unklarheiten die Oberstufenberatung kontaktieren."; }
        Refresh();
    }

    private void Refresh()
    {
        refreshing = true;
        try
        {
            stepTitle.Text = session.Step switch
            {
                1 => "1 · Drei Leistungsfächer wählen",
                2 => "2 · Zwei mündliche Prüfungsfächer planen",
                3 => "3 · Automatisch ergänzte Pflichtbasisfächer",
                _ => "4 · Weitere Wahlen und Halbjahre ergänzen"
            };
            stepInfo.Text = session.Step switch
            {
                1 => session.StepError ?? "LF-Teilprüfung ohne erkannten Fehler. Vorläufig: Persönliche Voraussetzungen und vollständige Rechtsprüfung bleiben needs_review.",
                2 => "Deutsch und Mathematik werden ohne entsprechendes LF automatisch festgelegt. Die Planung ist unverbindlich.\n"
                    + (session.StepError ?? "Planung deckt D/M und Aufgabenfelder ab; Zulassung und Anrechnung bleiben needs_review."),
                3 => session.AutomaticMessage + "\n\n" + string.Join("\n", session.PlanningNotes),
                _ => string.Join("\n", session.PlanningNotes)
            };
            previous.IsEnabled = !session.IsBusy && session.Step > 1;
            next.IsVisible = session.Step < 4;
            next.IsEnabled = !session.IsBusy && session.StepError is null;
            next.Content = session.Step == 2 ? "Pflichtbasisfächer _ergänzen" : "_Weiter";
            oralRows.Children.Clear();
            oralRows.IsVisible = session.Step == 2;
            if (session.Step == 2)
                foreach (var subject in session.OralCandidates.Union(session.Choice.PlannedOralSubjects))
                {
                    var box = new CheckBox
                    {
                        Name = "Oral_" + subject,
                        Content = (session.Catalog.Subjects.FirstOrDefault(s => s.Id == subject)?.Name ?? subject)
                            + (session.ForcedOral.Contains(subject) ? " · automatisch erforderlich" : ""),
                        IsChecked = session.Choice.PlannedOralSubjects.Contains(subject),
                        IsEnabled = !session.IsBusy && !session.ForcedOral.Contains(subject)
                    };
                    box.IsCheckedChanged += (_, _) => { if (!refreshing) Edit(() => session.SetOral(subject, box.IsChecked == true)); };
                    oralRows.Children.Add(box);
                }
            foreach (var offer in session.Catalog.Offers)
            {
                var (selected, semesters) = boxes[offer.Id];
                var choice = session.Choice.Selections.FirstOrDefault(s => s.OfferId == offer.Id);
                rows[offer.Id].IsVisible = session.Step == 1 ? offer.Type == CourseType.Lf || choice is not null
                    : session.Step == 3 ? choice is not null && offer.Type != CourseType.Lf
                    : session.Step == 4 && offer.Type != CourseType.Lf;
                selected.IsChecked = choice is not null;
                selected.IsEnabled = !session.IsBusy && session.Step != 3 && (choice is not null || session.Availability(offer).CanSelect);
                foreach (var box in semesters)
                {
                    box.IsChecked = choice?.Semesters.Contains((int)box.Tag!) == true;
                    box.IsEnabled = !session.IsBusy && session.Step == 4 && choice is not null && offer.Type != CourseType.Lf && session.Availability(offer).CanSelect;
                }
            }
            save.IsEnabled = !session.IsBusy && !session.LoadFailed;
            load.IsEnabled = !session.IsBusy;
            summary.Text = string.Join("\n", session.Summary.Select(s => $"HJ {s.Semester}: {s.CourseCount} Kurse · {s.WeeklyHours} Wochenstunden"))
                + $"\n\nGesamt: {session.Summary.Sum(s => s.CourseCount)} Halbjahreskurse";
            var result = session.Validation;
            validation.Text = (result.Status == ReviewStatus.Invalid ? "Fehler in der Belegung (invalid)" : "Fachlich zu klären (needs_review)")
                + "\n\n" + string.Join("\n\n", result.Messages.Select(m => $"{m.Rule.Id}: {m.Explanation}\n{m.Remedy}"));
            fileStatus.Text = session.IsBusy ? "Dateivorgang läuft …" : session.Message;
        }
        finally { refreshing = false; }
    }

    private async Task<bool> Confirm(string message)
    {
        var dialog = new Window { Title = "Entwurf schützen", Width = 480, Height = 220, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(22), Spacing = 18 };
        panel.Children.Add(Text(message, 16));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var cancel = new Button { Content = "Abbrechen" }; var discard = new Button { Content = "Änderungen verwerfen" };
        cancel.Click += (_, _) => dialog.Close(false); discard.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel); buttons.Children.Add(discard); panel.Children.Add(buttons); dialog.Content = panel;
        return await dialog.ShowDialog<bool>(this);
    }

    private static TextBlock Text(string value, double size, FontWeight? weight = null) => new()
    { Text = value, FontSize = size, FontWeight = weight ?? FontWeight.Normal, TextWrapping = TextWrapping.Wrap };
}
