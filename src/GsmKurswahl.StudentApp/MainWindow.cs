using System.Collections.Immutable;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;

namespace GsmKurswahl.StudentApp;

// Guided layout: stepper on top, one task area on the left, the permanent plan on the right.
// All decisions come from StudentSession/ChoiceWorkflow; this class only arranges and forwards them.
public sealed class MainWindow : Window
{
    private static readonly IBrush Page = Brush("#F4F6FA"), Card = Brushes.White, Line = Brush("#DDE1E8"),
        Muted = Brush("#5F5E5A"), Accent = Brush("#185FA5"), AccentSoft = Brush("#E6F1FB"),
        Done = Brush("#3B6D11"), DoneSoft = Brush("#EAF3DE"), Warn = Brush("#854F0B"), WarnSoft = Brush("#FAEEDA"),
        Danger = Brush("#A32D2D"), LfFill = Brush("#534AB7"), BfFill = Brush("#9FE1CB"), OtherFill = Brush("#F5C4B3"), Empty = Brush("#EEF0F4");
    private static readonly string[] StepNames = ["Leistungsfächer", "Prüfungsfächer", "Pflichtbereiche", "Weitere Kurse", "Überblick"];

    private readonly StudentSession session;
    private readonly WrapPanel stepper = new();
    private readonly StackPanel content = new() { Spacing = 12 };
    private readonly StackPanel plan = new() { Spacing = 10 };
    private readonly TextBlock fileStatus = Text("", 13);
    private readonly TextBlock error = Text("", 14);
    private readonly Border hoursWarning = Notice("", WarnSoft, Warn);
    private readonly Button previous = new() { Content = "_Zurück", Name = "PreviousStep" };
    private readonly Button next = new() { Content = "_Weiter", Name = "NextStep", Background = Accent, Foreground = Brushes.White };
    private readonly Button save = new() { Content = "Entwurf _speichern", Name = "SaveButton" };
    private readonly Button load = new() { Content = "Entwurf _laden", Name = "LoadButton" };
    private readonly CheckBox autosave = new() { Content = "Automatisch zwischenspeichern", IsChecked = true };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private string? expandedTask;
    private bool closeConfirmed;
    private bool closePromptOpen;

    public MainWindow(StudentSession session, string filePath, string account)
    {
        this.session = session;
        Title = "GSM-Kurswahl | Schülerprogramm · Demo";
        Width = 1440; Height = 900; MinWidth = 900; MinHeight = 600;
        WindowState = WindowState.Maximized;
        Background = Page;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), Margin = new Thickness(20), MaxWidth = 1760 };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), Margin = new Thickness(0, 0, 0, 12) };
        var title = new StackPanel { Spacing = 2 };
        var titleLine = new WrapPanel();
        titleLine.Children.Add(Text("GSM-Kurswahl", 26, FontWeight.Bold));
        var badge = Pill("Vorläufig · nicht eingereicht", WarnSoft, Warn);
        badge.VerticalAlignment = VerticalAlignment.Center;
        badge.Margin = new Thickness(12, 4, 0, 4);
        ToolTip.SetTip(badge, "Die automatische Prüfung unterstützt deine Planung. Die Oberstufenberatung muss den Regelstand für deinen Abiturjahrgang fachlich freigeben und offene Einzelfragen klären. In dieser Demo ist keine verbindliche Abgabe möglich.");
        titleLine.Children.Add(badge);
        title.Children.Add(titleLine);
        title.Children.Add(Text($"Synthetische Person {session.Choice.StudentId} · Abitur {session.Catalog.GraduationYear} · Demoangebot ohne schulische Freigabe", 13, color: Muted));
        header.Children.Add(title);
        var toolbar = new StackPanel { Name = "FileToolbar", Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        title.Name = "WindowHeading";
        toolbar.Children.Add(autosave); toolbar.Children.Add(load); toolbar.Children.Add(save);
        Grid.SetColumn(toolbar, 1); header.Children.Add(toolbar);
        root.Children.Add(header);
        void AdaptHeader()
        {
            var compact = ClientSize.Width < 1450;
            Grid.SetColumnSpan(title, compact ? 2 : 1);
            Grid.SetRow(toolbar, compact ? 1 : 0);
            Grid.SetColumn(toolbar, compact ? 0 : 1);
            Grid.SetColumnSpan(toolbar, compact ? 2 : 1);
            toolbar.Margin = new Thickness(0, compact ? 8 : 0, 0, 0);
        }
        SizeChanged += (_, _) => AdaptHeader();
        AdaptHeader();

        Grid.SetRow(stepper, 1); stepper.Margin = new Thickness(0, 0, 0, 12); root.Children.Add(stepper);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*") };
        var left = new DockPanel();
        var navigation = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 12, 0, 0) };
        navigation.Children.Add(previous);
        Grid.SetColumn(next, 2); navigation.Children.Add(next);
        DockPanel.SetDock(navigation, Dock.Bottom); left.Children.Add(navigation);
        left.Children.Add(Panel(new ScrollViewer { Content = content }));
        left.Margin = new Thickness(0, 0, 16, 0);
        body.Children.Add(left);
        var right = Panel(new ScrollViewer { Content = plan });
        Grid.SetColumn(right, 1); body.Children.Add(right);
        Grid.SetRow(body, 2); root.Children.Add(body);

        var footer = new StackPanel { Spacing = 3, Margin = new Thickness(0, 10, 0, 0) };
        hoursWarning.Name = "HoursWarning";
        footer.Children.Add(hoursWarning);
        error.Foreground = Danger;
        footer.Children.Add(error); footer.Children.Add(fileStatus);
        footer.Children.Add(PrerequisitePanel());
        var fileDetails = Text($"Datei: {filePath} · Betriebssystemkonto: {account} (keine bestätigte Schulzuordnung)", 11, color: Muted);
        fileDetails.TextWrapping = TextWrapping.NoWrap;
        fileDetails.TextTrimming = TextTrimming.CharacterEllipsis;
        ToolTip.SetTip(fileDetails, fileDetails.Text);
        footer.Children.Add(fileDetails);
        Grid.SetRow(footer, 3); root.Children.Add(footer);
        Content = root;

        previous.Click += (_, _) => Edit(session.PreviousStep);
        next.Click += (_, _) => Edit(() => { expandedTask = null; session.NextStep(); });
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
        timer.Stop();
        try
        {
            error.Text = ""; edit();
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

    // Controls are rebuilt from the session state; keyboard focus is restored by control name.
    private void Refresh()
    {
        var focused = (FocusManager?.GetFocusedElement() as Control)?.Name;
        BuildStepper();
        content.Children.Clear();
        content.Children.Add(session.Step switch { 1 => LfStep(), 2 => OralStep(), 3 => AreaStep(), 4 => ExtraStep(), _ => ReviewStep() });
        BuildPlan();
        var hoursMessage = session.Validation.Messages.FirstOrDefault(m => m.Rule.Id == "GSM-HOURS-UPPER");
        hoursWarning.IsVisible = hoursMessage is not null;
        ((TextBlock)hoursWarning.Child!).Text = hoursMessage?.Explanation ?? "";
        previous.IsEnabled = !session.IsBusy && session.Step > 1;
        next.IsVisible = session.Step < StudentSession.LastStep;
        next.IsEnabled = !session.IsBusy && session.StepError is null;
        next.Content = session.Step switch { 2 => "Pflichtfächer _eintragen", 3 when session.Tasks.Any(t => !t.IsSatisfied) => "Später entscheiden, _weiter", _ => "_Weiter" };
        save.IsEnabled = !session.IsBusy && !session.LoadFailed;
        load.IsEnabled = !session.IsBusy;
        fileStatus.Text = session.IsBusy ? "Dateivorgang läuft …" : session.Message;
        if (focused is not null && content.GetLogicalDescendants().OfType<Control>()
            .Concat(plan.GetLogicalDescendants().OfType<Control>())
            .Concat(stepper.Children).FirstOrDefault(c => c.Name == focused) is { } target)
            target.Focus();
    }

    private void BuildStepper()
    {
        stepper.Children.Clear();
        var open = session.Tasks.Count(t => !t.IsSatisfied);
        for (var i = 1; i <= StudentSession.LastStep; i++)
        {
            var step = i;
            var label = StepNames[i - 1] + (i == 3 && session.Step >= 3 && open > 0 ? $" · {open} offen" : "");
            var current = i == session.Step;
            var done = i < session.Step;
            var text = (done ? "✓ " : $"{i} ") + label;
            if (!done || session.IsBusy)
            {
                // Not clickable: a plain label, so future steps do not look like disabled buttons.
                stepper.Children.Add(new Border
                {
                    Name = "Step_" + i, Padding = new Thickness(12, 6), Margin = new Thickness(0, 0, 6, 4), CornerRadius = new CornerRadius(4),
                    Background = current ? AccentSoft : Brushes.Transparent, BorderBrush = current ? Accent : Line, BorderThickness = new Thickness(current ? 1.5 : 1),
                    Child = Text(text, 14, current ? FontWeight.SemiBold : FontWeight.Normal, current ? Accent : Muted)
                });
                continue;
            }
            var button = new Button { Name = "Step_" + i, Content = text, Background = DoneSoft, Foreground = Done, Margin = new Thickness(0, 0, 6, 4) };
            ToolTip.SetTip(button, $"Zurück zu Schritt {step}");
            button.Click += (_, _) => Edit(() => { expandedTask = null; session.GoToStep(step); });
            stepper.Children.Add(button);
        }
    }

    private Control LfStep()
    {
        var panel = Step("Wähle drei Leistungsfächer", "Mindestens zwei davon aus Deutsch, Mathematik, einer fortgeführten Fremdsprache oder einer Naturwissenschaft. Leistungsfächer laufen immer über HJ 1–4.");
        panel.Children.Add(Text("Wenn du ein bisheriges Basisfach als Leistungsfach wählst, wird seine Basisfachbelegung automatisch entfernt. Gehe anschließend die weiteren Schritte erneut durch.", 13, color: Muted));
        if (session.LfChangeMessage.Length > 0) panel.Children.Add(Notice(session.LfChangeMessage, WarnSoft, Warn));
        var chosen = session.Choice.Selections.Where(s => s.Type == CourseType.Lf).ToArray();
        panel.Children.Add(Text(chosen.Length < 3 ? $"{chosen.Length} von 3 gewählt – noch {3 - chosen.Length}" : $"{chosen.Length} von 3 gewählt · zum Tauschen zuerst ein Leistungsfach abwählen", 15, FontWeight.SemiBold));
        if (chosen.Length >= 3 && session.StepError is { } stepError) panel.Children.Add(Notice(stepError, WarnSoft, Warn));
        var offers = session.LfOffers.Concat(session.Catalog.Offers.Where(o => o.Type == CourseType.Lf && chosen.Any(s => s.OfferId == o.Id))).Distinct();
        foreach (var offer in offers)
        {
            var isChosen = chosen.Any(s => s.OfferId == offer.Id);
            // A fourth LF cannot be checked; otherwise its basic course would be removed by accident.
            var box = new CheckBox { Name = ControlName("Offer", offer.Id), Content = OfferLabel(offer), IsChecked = isChosen, IsEnabled = !session.IsBusy && (isChosen || chosen.Length < 3) };
            box.IsCheckedChanged += (_, _) => Edit(() => session.SetSelection(offer.Id, box.IsChecked == true ? offer.Schedule.Select(h => h.Semester) : []));
            panel.Children.Add(Option(box, box.IsChecked == true));
        }
        AddRestricted(panel, session.RestrictedOffers(lf: true), "Leistungsfach-Angebote derzeit nicht wählbar");
        return panel;
    }

    private Control OralStep()
    {
        var panel = Step("Plane zwei mündliche Prüfungsfächer", "Zusammen mit deinen Leistungsfächern müssen Deutsch, Mathematik und alle drei Aufgabenfelder abgedeckt sein. Die Planung ist unverbindlich.");
        if (session.LfChangeMessage.Length > 0) panel.Children.Add(Notice(session.LfChangeMessage, WarnSoft, Warn));
        foreach (var subject in session.ForcedOral)
            panel.Children.Add(Notice($"✓ {session.SubjectName(subject)} ist gesetzt, weil du es nicht als Leistungsfach hast.", DoneSoft, Done));
        var planned = session.Choice.PlannedOralSubjects;
        var free = 2 - planned.Length;
        panel.Children.Add(Text(free > 0 ? $"Noch {free} Fach {(free == 1 ? "" : "bzw. 2 Fächer ")}wählen" : "Beide Prüfungsfächer geplant", 15, FontWeight.SemiBold));
        if (planned.Length == 2 && session.StepError is { } stepError) panel.Children.Add(Notice(stepError, WarnSoft, Warn));
        foreach (var subject in planned.Where(s => !session.ForcedOral.Contains(s)).Concat(session.UsefulOralCandidates).Distinct())
        {
            var box = new CheckBox { Name = "Oral_" + subject, Content = session.SubjectName(subject), IsChecked = planned.Contains(subject), IsEnabled = !session.IsBusy };
            box.IsCheckedChanged += (_, _) => Edit(() => session.SetOral(subject, box.IsChecked == true));
            panel.Children.Add(Option(box, box.IsChecked == true));
        }
        var hidden = session.OralCandidates.Except(session.UsefulOralCandidates).Except(planned).Count();
        if (hidden > 0 && free > 0)
            panel.Children.Add(Text($"{hidden} weitere Fächer passen mit deiner bisherigen Planung nicht zu einer vollständigen Kombination und sind ausgeblendet.", 12, color: Muted));
        return panel;
    }

    private Control AreaStep()
    {
        var panel = Step("Pflichtbereiche", "Pflichtfächer sind eingetragen und im Plan mit „Pflicht“ markiert. Hier entscheidest du nur noch, wo es eine Auswahl gibt.");
        var automatic = session.Plan.Where(r => r.IsAutomatic && !r.IsOpen).Select(r => $"{r.Label} (HJ {Semesters(r.Semesters)})").ToArray();
        if (automatic.Length > 0) panel.Children.Add(Notice("✓ Eingetragen: " + string.Join(", ", automatic), DoneSoft, Done));
        if (session.AutomaticMessage.Length > 0) panel.Children.Add(Notice(session.AutomaticMessage, WarnSoft, Warn));
        foreach (var message in session.Validation.Messages.Where(m => m.Rule.Id == "AGVO-RELIGION"))
            panel.Children.Add(Notice(message.Explanation, WarnSoft, Warn));
        var tasks = session.Tasks;
        var expanded = expandedTask ?? tasks.FirstOrDefault(t => !t.IsSatisfied && t.Choose > 0)?.Id;
        foreach (var task in tasks)
        {
            if (task.Id == expanded && task.Choose > 0) { panel.Children.Add(TaskCard(task)); continue; }
            var answer = task.FulfilledBySubjects.Select(session.SubjectName)
                .Concat(session.SelectedTaskOptions(task).Select(id => session.SubjectName(session.Catalog.Offers.Single(o => o.Id == id).SubjectId))).ToArray();
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(Text(task.IsSatisfied ? $"✓ {task.Title}: {string.Join(", ", answer)}" : $"○ {task.Title} – offen", 14, color: task.IsSatisfied ? Done : Warn));
            if (task.Choose > 0)
            {
                var change = new Button { Name = ControlName("Task", task.Id), Content = task.IsSatisfied ? "Ändern" : "Jetzt wählen", IsEnabled = !session.IsBusy };
                change.Click += (_, _) => { expandedTask = task.Id; Refresh(); };
                Grid.SetColumn(change, 1); row.Children.Add(change);
            }
            panel.Children.Add(new Border { Child = row, Padding = new Thickness(10, 6), BorderBrush = Line, BorderThickness = new Thickness(0, 0, 0, 1) });
        }
        return panel;
    }

    private Control TaskCard(ChoiceTask task)
    {
        var card = new StackPanel { Spacing = 8 };
        var open = session.Tasks.Count(t => !t.IsSatisfied);
        card.Children.Add(Text(task.IsSatisfied ? "Antwort ändern" : $"Offene Frage · noch {open} offen", 12, color: Muted));
        card.Children.Add(Text(task.Title + (task.Choose > 1 ? $" – wähle {task.Choose}" : ""), 18, FontWeight.SemiBold));
        if (task.FulfilledBySubjects.Length > 0)
            card.Children.Add(Text("Bereits belegt: " + string.Join(", ", task.FulfilledBySubjects.Select(session.SubjectName)), 13, color: Done));
        card.Children.Add(Text(task.Hint, 13, color: Muted));
        // Every course already selected for the whole period is shown as chosen. Radio buttons only while
        // a new answer simply replaces the single visible one; otherwise check boxes add or remove explicitly.
        var selected = session.SelectedTaskOptions(task);
        var replaces = session.ReplacesTaskAnswer(task);
        if (selected.Length > 1)
            card.Children.Add(Text("Du hast mehrere passende Kurse belegt. Abwählen entfernt den jeweiligen Kurs; andere Kurse bleiben erhalten.", 13, color: Muted));
        foreach (var offerId in task.OptionOfferIds.Union(task.ChosenOfferIds))
        {
            var offer = session.Catalog.Offers.Single(o => o.Id == offerId);
            var chosen = selected.Contains(offerId);
            var locked = chosen && session.IsRequiredSubject(offer.SubjectId);
            CreateToggle(out var toggle, replaces, ControlName("Option", offerId), OfferLabel(offer) + (locked ? " · durch Prüfungsplanung gesetzt" : ""), chosen, task.Id);
            toggle.IsEnabled = !session.IsBusy && !locked && (chosen || task.OptionOfferIds.Contains(offerId));
            toggle.IsCheckedChanged += (_, _) =>
            {
                // A radio group unchecks the previous answer itself; ChooseTaskOption replaces it in the model.
                if (toggle is RadioButton && toggle.IsChecked != true) return;
                Edit(() => session.ChooseTaskOption(task.Id, offerId, toggle.IsChecked == true));
            };
            card.Children.Add(Option(toggle, chosen));
        }
        AddRestricted(card, [.. task.Restricted.Select(r => session.Catalog.Offers.Single(o => o.Id == r.OfferId))], "weitere Angebote brauchen ein geprüftes Profil oder sind nicht freigegeben");
        if (task.IsSatisfied && expandedTask == task.Id)
        {
            var close = new Button { Name = "TaskDone", Content = "Fertig" };
            close.Click += (_, _) => { expandedTask = null; Refresh(); };
            card.Children.Add(close);
        }
        return new Border { Child = card, Padding = new Thickness(14), CornerRadius = new CornerRadius(8), BorderBrush = Accent, BorderThickness = new Thickness(1.5) };
    }

    private Control ExtraStep()
    {
        var panel = Step("Weitere Kurse", "Ergänze Kurse, bis du das Planungsziel erreichst. Regulär wählst du ganze Schuljahre (HJ 1+2 bzw. HJ 3+4). Einzelhalbjahre werden nur bei einer passenden Ausnahme angeboten.");
        panel.Children.Add(Text(Progress(), 15, FontWeight.SemiBold));
        var adjustable = session.Choice.Selections.Where(s => s.Type != CourseType.Lf).Select(s => (Selection: s, Offer: session.Catalog.Offers.FirstOrDefault(o => o.Id == s.OfferId)))
            .Where(x => x.Offer is not null && (!session.LockedSemesters(x.Selection.OfferId).Any() || x.Offer.Schedule.Length > session.LockedSemesters(x.Selection.OfferId).Length)).ToArray();
        if (adjustable.Length > 0)
        {
            panel.Children.Add(Text("Deine Kurse anpassen", 15, FontWeight.SemiBold));
            foreach (var (selection, offer) in adjustable) panel.Children.Add(SelectionRow(selection, offer!));
        }
        var extras = session.ExtraOffers;
        if (extras.Length > 0)
        {
            panel.Children.Add(Text("Kurs hinzufügen", 15, FontWeight.SemiBold));
            foreach (var offer in extras)
            {
                var box = new CheckBox { Name = ControlName("Offer", offer.Id), Content = OfferLabel(offer), IsEnabled = !session.IsBusy };
                box.IsCheckedChanged += (_, _) => Edit(() => session.SetSelection(offer.Id, offer.Schedule.Select(h => h.Semester)));
                panel.Children.Add(Option(box, false));
            }
        }
        AddRestricted(panel, session.RestrictedOffers(lf: false), "weitere Angebote derzeit nicht wählbar");
        return panel;
    }

    private Control SelectionRow(CourseSelection selection, CourseOffer offer)
    {
        var locked = session.LockedSemesters(selection.OfferId);
        var row = new StackPanel { Spacing = 4 };
        var removable = locked.IsEmpty;
        if (removable)
        {
            var box = new CheckBox { Name = ControlName("Offer", offer.Id), Content = OfferLabel(offer), IsChecked = true, IsEnabled = !session.IsBusy };
            box.IsCheckedChanged += (_, _) => Edit(() => session.SetSelection(offer.Id, []));
            row.Children.Add(box);
        }
        else row.Children.Add(Text(OfferLabel(offer) + " · Pflicht in HJ " + Semesters(locked), 14));
        var semesters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(28, 0, 0, 0) };
        foreach (var unit in session.SemesterSelectionUnits(offer.Id))
        {
            var box = new CheckBox
            {
                Name = ControlName("Sem", offer.Id) + "_" + string.Join("_", unit), Content = "HJ " + string.Join("+", unit), FontSize = 12,
                IsChecked = unit.All(selection.Semesters.Contains),
                IsEnabled = !session.IsBusy && offer.Type != CourseType.Seminar && !unit.Any(locked.Contains)
                    && unit.All(h => offer.Schedule.Any(s => s.Semester == h))
            };
            box.IsCheckedChanged += (_, _) => Edit(() =>
                session.SetSelection(offer.Id, box.IsChecked == true ? selection.Semesters.Union(unit) : selection.Semesters.Except(unit)));
            semesters.Children.Add(box);
        }
        row.Children.Add(semesters);
        return Option(row, true);
    }

    private Control ReviewStep()
    {
        var panel = Step("Überblick und offene Punkte", "Deine Wahl wird automatisch geprüft. Du kannst den Entwurf speichern und den Kontrollbogen exportieren. In dieser Demo ist keine verbindliche Abgabe möglich; die fachliche Freigabe für deinen Abiturjahrgang steht noch aus.");
        panel.Children.Add(Text("Wer klärt offene Punkte? Die Oberstufenberatung prüft unklare persönliche Voraussetzungen und Sonderfälle sowie die Anrechnung. Du ergänzt fehlende Kurse und besprichst die hier aufgeführten Rückfragen mit ihr. Ein Prüfhinweis bedeutet noch nicht, dass deine Wahl falsch ist.", 14, color: Muted));
        foreach (var message in session.Validation.Messages.Where(m => m.Rule.Id == "AGVO-RELIGION"))
            panel.Children.Add(Notice(message.Explanation, WarnSoft, Warn));
        panel.Children.Add(Text(Progress(), 15, FontWeight.SemiBold));
        var table = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"), Margin = new Thickness(0, 4) };
        var summary = session.Summary;
        for (var i = 0; i < summary.Length; i++)
        {
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Cell(table, $"HJ {summary[i].Semester}", i, 0); Cell(table, $"{summary[i].CourseCount} Kurse", i, 1); Cell(table, $"{summary[i].WeeklyHours} Wochenstunden", i, 2);
        }
        panel.Children.Add(table);
        var export = new Button { Content = "Kontrollbogen als _PDF exportieren", Name = "ControlSheetButton" };
        export.Click += async (_, _) => await RunFileAction(async () =>
        {
            var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Kontrollbogen speichern", SuggestedFileName = "kurswahl-kontrollbogen.pdf", DefaultExtension = "pdf",
                FileTypeChoices = [new("PDF-Kontrollbogen") { Patterns = ["*.pdf"] }], ShowOverwritePrompt = true
            });
            if (file is null) return;
            if (!file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Kontrollbögen müssen als .pdf gespeichert werden; Wahl- und Profildateien bleiben geschützt.");
            var pdf = GsmKurswahl.ImportExport.ControlSheet.Render(session.Choice, session.Catalog,
                session.Profile, session.Validation, session.Summary, session.ExamOptions);
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await stream.WriteAsync(pdf);
        });
        panel.Children.Add(export);
        panel.Children.Add(Text("PDF-Export mit dem aktuellen Entwurf und allen Prüfhinweisen.", 12, color: Muted));
        panel.Children.Add(Text("Mögliche mündliche Kombinationen aus deiner Belegung", 15, FontWeight.SemiBold));
        foreach (var exam in session.ExamOptions)
            panel.Children.Add(Text($"{session.SubjectName(exam.First)} + {session.SubjectName(exam.Second)} · vorläufige Mindestanrechnung {exam.MinimumCredits}", 13));
        panel.Children.Add(Text("Die Oberstufenberatung bestätigt die persönlichen Prüfungsvoraussetzungen und die endgültige Anrechnung. Diese Kombinationen sind Planungsvorschläge.", 12, color: Muted));
        if (session.ExamOptions.IsEmpty) panel.Children.Add(Text("Noch keine passende Kombination aus den belegten Kursen. Pflichtbelegung und offene Punkte prüfen.", 13));
        panel.Children.Add(Text("Noch zu erledigen oder zu klären", 15, FontWeight.SemiBold));
        foreach (var note in session.PlanningNotes) panel.Children.Add(Text("• " + note, 13));
        var messages = session.Validation.Messages;
        var details = new StackPanel { Spacing = 6 };
        foreach (var m in messages) details.Children.Add(Text($"{m.Rule.Id}: {m.Explanation}\n{m.Remedy}", 12, color: m.Rule.Severity == ReviewStatus.Invalid ? Danger : Muted));
        panel.Children.Add(new Expander { Header = $"Prüfhinweise ({messages.Length}) – {(session.Validation.Status == ReviewStatus.Invalid ? "Wahl bitte korrigieren" : "Klärung durch die Oberstufenberatung erforderlich")}", Content = details, HorizontalAlignment = HorizontalAlignment.Stretch });
        return panel;
    }

    private void BuildPlan()
    {
        plan.Children.Clear();
        plan.Children.Add(Text("Dein Plan", 18, FontWeight.Bold));
        var rows = session.Plan;
        if (rows.IsEmpty) { plan.Children.Add(Text("Noch nichts gewählt. Beginne mit deinen Leistungsfächern.", 13, color: Muted)); }
        else
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,40,40,40,40") };
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var h = 1; h <= 4; h++) Cell(grid, $"HJ {h}", 0, h, center: true);
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var label = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
                label.Children.Add(Text(row.Label, 14, color: row.IsOpen ? Warn : null));
                if (row.Type == CourseType.Lf) label.Children.Add(Badge("LF", LfFill, Brushes.White));
                if (row.Variant == CourseVariant.Bilingual) label.Children.Add(Badge("bili", Empty, Muted));
                if (row.IsAutomatic && !row.IsOpen) label.Children.Add(Badge("Pflicht", Empty, Muted));
                if (row.IsOral) label.Children.Add(Badge("mündl.", AccentSoft, Accent));
                if (row.Issue == ReviewStatus.Invalid) label.Children.Add(Badge("Fehler", Brush("#FCEBEB"), Danger, row.IssueText));
                else if (row.Issue == ReviewStatus.NeedsReview && !row.IsOpen) label.Children.Add(Badge("klären", WarnSoft, Warn, row.IssueText));
                if (row.IssueText is { Length: > 0 }) ToolTip.SetTip(label, row.IssueText);
                foreach (var item in label.Children) item.Margin = new Thickness(0, 2, 5, 2);
                Control jump = label;
                if (!session.IsBusy && row.Step < session.Step)
                {
                    var target = row.Step;
                    var button = new Button
                    {
                        Name = "Plan_" + i, Content = label, Background = Brushes.Transparent, Padding = new Thickness(2, 1),
                        HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left
                    };
                    button.Click += (_, _) => Edit(() => { expandedTask = null; session.GoToStep(target); });
                    ToolTip.SetTip(button, row.IssueText is { Length: > 0 } ? $"{row.IssueText}\n\nKlicken, um zu Schritt {target} zu springen." : $"Zu Schritt {target} springen");
                    jump = button;
                }
                else label.Margin = new Thickness(3, 2);
                Grid.SetRow(jump, i + 1); grid.Children.Add(jump);
                for (var h = 1; h <= 4; h++)
                {
                    Control cell = row.IsOpen
                        ? new Rectangle { Stroke = Muted, StrokeThickness = 1, StrokeDashArray = new AvaloniaList<double>(3, 2), Fill = Brushes.Transparent, IsVisible = row.Semesters.Contains(h) }
                        : new Border { CornerRadius = new CornerRadius(3), Background = !row.Semesters.Contains(h) ? Empty : row.Type == CourseType.Lf ? LfFill : row.Type == CourseType.Bf ? BfFill : OtherFill };
                    cell.Height = 18; cell.Margin = new Thickness(2);
                    Grid.SetRow(cell, i + 1); Grid.SetColumn(cell, h); grid.Children.Add(cell);
                }
            }
            plan.Children.Add(grid);
        }
        var legend = new WrapPanel();
        foreach (var (name, fill) in new[] { ("Leistungsfach", LfFill), ("Basisfach", BfFill), ("Wahlfach/Seminar", OtherFill) })
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 0, 12, 4) };
            item.Children.Add(new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(2), Background = fill });
            item.Children.Add(Text(name, 11, color: Muted));
            legend.Children.Add(item);
        }
        legend.Children.Add(Text("gestrichelt = noch offen", 11, color: Muted));
        plan.Children.Add(legend);
        plan.Children.Add(Text(Progress(), 14, FontWeight.SemiBold));
        plan.Children.Add(new ProgressBar { Minimum = 0, Maximum = ChoiceWorkflow.MinimumCourses, Value = Math.Min(session.CountedCourses, ChoiceWorkflow.MinimumCourses), Height = 8 });
        var semesterTotals = new WrapPanel();
        foreach (var semester in session.Summary)
        {
            var total = Text($"HJ {semester.Semester}: {semester.CourseCount} Kurse · {semester.WeeklyHours} Wochenstunden", 14, FontWeight.SemiBold);
            total.Margin = new Thickness(0, 2, 14, 2);
            semesterTotals.Children.Add(total);
        }
        plan.Children.Add(semesterTotals);
        plan.Children.Add(Text("Belegungszahlen und Wochenstunden sind keine Anrechnungs- oder Zulassungsbestätigung.", 11, color: Muted));
    }

    // Shown read-only: students cannot declare prerequisites themselves (Pflichtenheft §5, §5a).
    private Control PrerequisitePanel()
    {
        var list = new StackPanel { Spacing = 3 };
        list.Children.Add(Text(session.ProfileStatusText, 11, color: Muted));
        foreach (var item in session.Prerequisites)
        {
            list.Children.Add(Text($"{item.Label}: {item.Value}.", 11, color: Muted));
        }
        list.Children.Add(Text("Änderungen und Rückfragen: Oberstufenberatung.", 11, color: Muted));
        var expander = new Expander
        {
            Name = "Prerequisites",
            Header = Text(session.Profile.Status switch
            {
                ProfileStatus.Verified => "Voraussetzungen · Profil geprüft · Details",
                ProfileStatus.ProfileInvalid => "Voraussetzungen · Profil ungültig · Oberstufenberatung kontaktieren",
                _ => "Voraussetzungen ungeprüft · Details bei Bedarf"
            }, 11, color: Muted),
            Content = new ScrollViewer { Content = list, MaxHeight = 140 },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(0), MinHeight = 0, IsExpanded = false
        };
        return expander;
    }

    private string Progress()
    {
        var count = session.CountedCourses;
        var missing = ChoiceWorkflow.MinimumCourses - count;
        return $"{count} von {ChoiceWorkflow.MinimumCourses} Halbjahreskursen" + (missing > 0 ? $" – noch {missing}" : " – Planungsziel erreicht (nicht geprüft)");
    }

    private void AddRestricted(StackPanel panel, ImmutableArray<CourseOffer> offers, string header)
    {
        if (offers.IsEmpty) return;
        var list = new StackPanel { Spacing = 4 };
        foreach (var offer in offers) list.Children.Add(Text($"{OfferLabel(offer)}: {session.Availability(offer).Explanation}", 12, color: Muted));
        panel.Children.Add(new Expander { Header = $"{offers.Length} {header}", Content = list, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 });
    }

    private string OfferLabel(CourseOffer offer)
    {
        var type = offer.Type switch { CourseType.Lf => "LF", CourseType.Bf => "BF", CourseType.Elective => "Wahlfach", _ => "Seminarkurs" };
        var hours = offer.Schedule.Select(h => h.WeeklyHours).Distinct().ToArray();
        return $"{session.SubjectName(offer.SubjectId)} · {type}" + (offer.Variant == CourseVariant.Bilingual ? " · bilingual" : "")
            + $" · {string.Join("/", hours)} h · HJ {Semesters(offer.Schedule.Select(h => h.Semester))}";
    }

    private static string Semesters(IEnumerable<int> semesters)
    {
        var list = semesters.Order().ToArray();
        return list.Length > 2 && list[^1] - list[0] == list.Length - 1 ? $"{list[0]}–{list[^1]}" : string.Join(", ", list);
    }

    private static string ControlName(string prefix, string id) => prefix + "_" + id.Replace('-', '_');

    private static void CreateToggle(out ToggleButton toggle, bool exclusive, string name, string label, bool isChecked, string group)
    {
        toggle = exclusive ? new RadioButton { GroupName = group } : new CheckBox();
        toggle.Name = name; toggle.Content = label; toggle.IsChecked = isChecked;
    }

    private static StackPanel Step(string title, string hint)
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(Text(title, 22, FontWeight.Bold));
        panel.Children.Add(Text(hint, 14, color: Muted));
        return panel;
    }

    private static Border Panel(Control child) => new()
    { Child = child, Background = Card, CornerRadius = new CornerRadius(10), BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(18) };

    private static Border Option(Control child, bool selected) => new()
    {
        Child = child, Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(6),
        BorderBrush = selected ? Accent : Line, BorderThickness = new Thickness(selected ? 1.5 : 1), Background = selected ? AccentSoft : Card
    };

    private static Border Notice(string text, IBrush background, IBrush foreground) => new()
    { Child = Text(text, 13, color: foreground), Background = background, CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6) };

    private static Border Pill(string text, IBrush background, IBrush foreground) => new()
    { Child = Text(text, 12, FontWeight.SemiBold, foreground), Background = background, CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 3) };

    private static Border Badge(string text, IBrush background, IBrush foreground, string? tooltip = null)
    {
        var border = new Border
        { Child = Text(text, 10, FontWeight.SemiBold, foreground), Background = background, CornerRadius = new CornerRadius(3), Padding = new Thickness(4, 0), VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrWhiteSpace(tooltip)) ToolTip.SetTip(border, tooltip);
        return border;
    }

    private static void Cell(Grid grid, string text, int row, int column, bool center = false)
    {
        var block = Text(text, 12, color: Muted);
        block.Margin = new Thickness(0, 0, 14, 2);
        if (center) { block.HorizontalAlignment = HorizontalAlignment.Center; block.Margin = new Thickness(0, 0, 0, 2); }
        Grid.SetRow(block, row); Grid.SetColumn(block, column); grid.Children.Add(block);
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

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    private static TextBlock Text(string value, double size, FontWeight? weight = null, IBrush? color = null)
    {
        var block = new TextBlock { Text = value.Replace("needs_review", "durch die Oberstufenberatung zu klären").Replace("profile_invalid", "Profil ungültig"), FontSize = size, FontWeight = weight ?? FontWeight.Normal, TextWrapping = TextWrapping.Wrap };
        if (color is not null) block.Foreground = color;
        return block;
    }
}
