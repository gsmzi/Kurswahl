using System.Collections.Immutable;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.ImportExport;
using GsmKurswahl.Rules;

namespace GsmKurswahl.TeacherApp;

// Page "Kurswahlen": after collection all choices live in the teacher stock. List with filters on the left,
// the selected pupil's choice as a semester grid on the right. Every decision comes from ChoiceOverview/ChoiceValidator.
public sealed partial class TeacherWindow
{
    private static readonly IBrush Red = Brush.Parse("#A32D2D"), RedSoft = Brush.Parse("#FCEBEB"),
        Yellow = Brush.Parse("#854F0B"), YellowSoft = Brush.Parse("#FAEEDA"), Green = Brush.Parse("#3B6D11"), GreenSoft = Brush.Parse("#EAF3DE");
    private ChoiceFilter choiceFilter = new();
    private string? choiceSelectedId;
    private sealed record FilterItem(string? Value, string Label) { public override string ToString() => Label; }

    private TeacherState[] YearStates() => [.. session!.Overview().Where(s => s.Record.Student.GraduationYear == session.Catalog.GraduationYear)];

    private void ShowResults()
    {
        Ready(); visiblePage = "results";
        var all = YearStates();
        var root = new Grid { Name = "ChoicePage" };
        var listPanel = new DockPanel();
        var top = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 8) };
        var summary = Text("");
        var search = new TextBox { Name = "ResultsSearch", Watermark = "Name, Klasse oder Schüler-ID", Width = 220, Text = choiceFilter.Text, Margin = new Thickness(0, 0, 8, 6) };
        var classItems = new[] { new FilterItem(null, "Alle Klassen") }.Concat(ChoiceOverview.Classes(all).Select(c => new FilterItem(c, "Klasse " + c))).ToArray();
        var classBox = new ComboBox { Name = "ResultsClassFilter", ItemsSource = classItems, SelectedItem = classItems.FirstOrDefault(i => i.Value == choiceFilter.ClassName) ?? classItems[0], MinWidth = 130, Margin = new Thickness(0, 0, 8, 6) };
        var stateBox = new ComboBox { Name = "ResultsStatusFilter", SelectedIndex = (int)choiceFilter.State, MinWidth = 200, Margin = new Thickness(0, 0, 8, 6),
            ItemsSource = new[] { "Alle Prüfstände", "Ohne Wahl", "Unvollständige Kurswahl", "Regelverletzung (rot)", "Zu prüfen (gelb)", "Formal gültig", "Nachwahl offen", "Feststellungsprüfung nötig" } };
        var offerItems = new[] { new FilterItem(null, "Alle Kurse") }.Concat(session!.Catalog.Offers
            .Where(o => o.Status is OfferStatus.Offered or OfferStatus.Established || all.Any(s => s.Active?.Snapshot.Selections.Any(c => c.OfferId == o.Id) == true))
            .OrderBy(o => SubjectName(o.SubjectId), StringComparer.CurrentCulture).ThenBy(o => o.Type).ThenBy(o => o.Variant)
            .Select(o => new FilterItem(o.Id, ChoiceOverview.OfferTitle(session.Catalog, o)))).ToArray();
        var offerBox = new ComboBox { Name = "ResultsCourseFilter", ItemsSource = offerItems, SelectedItem = offerItems.FirstOrDefault(i => i.Value == choiceFilter.OfferId) ?? offerItems[0], MinWidth = 210, Margin = new Thickness(0, 0, 8, 6) };
        var semesterBox = new ComboBox { Name = "ResultsSemesterFilter", ItemsSource = new[] { "Alle Halbjahre", "HJ 1", "HJ 2", "HJ 3", "HJ 4" }, SelectedIndex = choiceFilter.Semester ?? 0, MinWidth = 130, Margin = new Thickness(0, 0, 8, 6) };
        ToolTip.SetTip(stateBox, "Unvollständig: keine Wahl, fehlende Pflichtbelegung, weniger als 42 Kurse oder keine zwei mündlichen Fächer geplant · Rot: Regelverletzung · Gelb: Besonderheit, die die Oberstufenberatung klärt.");
        ToolTip.SetTip(offerBox, "Zeigt nur Personen, die diesen Kurs belegen. Zusammen mit dem Halbjahr entsteht die Kursliste.");
        var filters = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var control in new Control[] { search, classBox, stateBox, offerBox, semesterBox }) filters.Children.Add(control);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        TeacherState[] shown = [];
        void AddAction(string label, string name, Func<Task> action, string help)
        {
            var button = Button(label, action, help); button.Name = name; button.Margin = new Thickness(0, 0, 8, 6); button.Padding = new Thickness(10, 4); actions.Children.Add(button);
        }
        AddAction("Übersicht (PDF) …", "PrintOverview", () => SavePdf(OverviewPdf(shown), "Kurswahlen-Uebersicht"),
            "Eine Zeile je angezeigter Person: Leistungsfächer, Basis- und Wahlfächer, mündliche Planung, Prüfstand.");
        AddAction("Kontrollbögen (PDF) …", "PrintControlSheets", () => SavePdf(ControlSheetsPdf(shown), "Kontrollboegen"),
            "Ein Kontrollbogen je angezeigter Person in einer Datei, mit Unterschriftenzeile für die Schülerin oder den Schüler.");
        AddAction("Kurslisten (PDF) …", "PrintCourseLists", () => SavePdf(CourseListsPdf(shown), "Kurslisten"),
            "Mit gewähltem Kurs: die Liste dieses Kurses (ggf. nur das gewählte Halbjahr). Ohne Kursfilter: alle Kurslisten der angezeigten Personen, je Kurs eine Seite.");
        AddAction("Nachwahlbögen (PDF) …", "PrintReselections", () => SavePdf(ReselectionsPdf(shown), "Nachwahlboegen"),
            "Ein Nachwahlbogen je angezeigter Person mit offener Nachwahl, alle in einer Datei.");
        top.Children.Add(summary); top.Children.Add(filters); top.Children.Add(actions);
        var header = new ContentControl(); top.Children.Add(header);
        DockPanel.SetDock(top, Dock.Top); listPanel.Children.Add(top);
        var rows = new StackPanel { Spacing = 1, Name = "ResultRows" };
        listPanel.Children.Add(new ScrollViewer { Content = rows });
        var detail = new ContentControl { Name = "ChoiceDetail" };
        root.Children.Add(listPanel); root.Children.Add(detail);

        void Render()
        {
            all = YearStates();
            shown = [.. ChoiceOverview.Apply(all, choiceFilter)];
            var selected = choiceSelectedId is null ? null : all.FirstOrDefault(s => s.Record.Student.ExternalStudentId == choiceSelectedId);
            var open = selected is not null;
            root.ColumnDefinitions = new ColumnDefinitions(open ? "1.05*,14,*" : "*");
            Grid.SetColumn(detail, open ? 2 : 0); detail.IsVisible = open;
            detail.Content = open ? BuildChoiceEditor(selected!, Render) : null;
            var withChoice = all.Where(s => s.Active is not null).ToArray();
            summary.Text = $"{shown.Length} von {all.Length} Personen angezeigt · {withChoice.Length} Kurswahlen · {all.Length - withChoice.Length} ohne Wahl · "
                + $"{withChoice.Count(s => s.Validation?.Status == ReviewStatus.Invalid)} mit Regelverletzung · {withChoice.Count(s => s.Validation?.Status == ReviewStatus.NeedsReview)} zu prüfen · "
                + $"{all.Count(s => s.RequiresReselection)} Nachwahl offen. Name anklicken, um die Wahl zu bearbeiten.";
            var columns = open ? "175,45,120,48,*,115" : "175,45,125,*,48,80,165,125";
            header.Content = new Border { Background = SoftAccent, Child = CompactLine(columns, open
                ? [Text("Schüler"), Text("Kl."), Text("Leistungsfächer"), Text("Kurse"), Text("Prüfstand"), Text("Abgänger")]
                : [Text("Schüler"), Text("Kl."), Text("Leistungsfächer"), Text("Basis- und Wahlfächer"), Text("Kurse"), Text("Mündlich"), Text("Prüfstand"), Text("Abgänger")]) };
            rows.Children.Clear();
            var index = 0;
            foreach (var state in shown)
            {
                var record = state.Record; var id = record.Student.ExternalStudentId;
                var choice = state.Active?.Snapshot;
                var religion = ReligionCases.For(state);
                var name = new Button { Name = "ResultOpen_" + id, Content = record.Student.SyntheticDisplayName, Padding = new Thickness(4, 1), MinHeight = 24, Background = Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = id == choiceSelectedId ? FontWeight.SemiBold : FontWeight.Normal };
                ToolTip.SetTip(name, id + " · Kurswahl dieser Person anzeigen und bearbeiten");
                name.Click += (_, _) => { choiceSelectedId = id; Render(); };
                var review = Text(FormatStatus(state.Validation?.Status) + (state.RequiresReselection ? " · Nachwahl" : "") + (religion?.AssessmentSubject is { } fp ? " · FP " + fp : ""));
                review.Name = "ResultReview_" + id;
                review.Foreground = state.Validation?.Status switch { ReviewStatus.Invalid => Red, ReviewStatus.NeedsReview => Yellow, ReviewStatus.Valid => Green, _ => Muted };
                var hints = ChoiceOverview.Violations(state.Validation).Select(v => "Regelverletzung: " + v).Concat(ChoiceOverview.Hints(state.Validation).Select(h => "Zu prüfen: " + h)).ToArray();
                ToolTip.SetTip(review, new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 520,
                    Text = state.Active is null ? "Noch keine Kurswahl." : hints.Length == 0 ? "Keine offenen Punkte." : string.Join("\n\n", hints) });
                var departure = new ComboBox { Name = "ResultDeparture_" + id, ItemsSource = new[] { "Aktiv", "Voraussichtlich", "Bestätigt" }, SelectedIndex = (int)record.Student.Departure,
                    MinHeight = 28, Padding = new Thickness(6, 2), HorizontalAlignment = HorizontalAlignment.Stretch };
                var updating = false;
                departure.SelectionChanged += (_, _) =>
                {
                    if (updating || departure.SelectedIndex < 0 || departure.SelectedIndex == (int)record.Student.Departure) return;
                    try
                    {
                        var next = record with { Student = record.Student with { Departure = (DepartureStatus)departure.SelectedIndex }, Version = record.Version + 1, ChangedAt = DateTimeOffset.UtcNow, Actor = Actor };
                        store!.SaveStudent(next, record.Version); record = next;
                        Refresh(preserveEditor: true);
                        status.Text = record.Student.SyntheticDisplayName + ": Abgängerstatus gespeichert. Kursplanung berücksichtigt die Änderung.";
                    }
                    catch (Exception ex)
                    {
                        updating = true; departure.SelectedIndex = (int)record.Student.Departure; updating = false;
                        status.Text = "Abgängerstatus nicht gespeichert: " + ex.Message + " Kurswahlen erneut öffnen.";
                    }
                };
                var lf = choice is null ? "–" : PlanningPresentation.Choices(choice, true, session!.Catalog);
                var count = Text(choice is null ? "–" : choice.Selections.Sum(s => s.Semesters.Length).ToString());
                if (choice is not null && choice.Selections.Sum(s => s.Semesters.Length) < ChoiceWorkflow.MinimumCourses) count.Foreground = Red;
                Control[] cells = open
                    ? [name, Text(record.Student.ClassName), Text(lf), count, review, departure]
                    : [name, Text(record.Student.ClassName), Text(lf), Text(choice is null ? "" : PlanningPresentation.Choices(choice, false, session!.Catalog).Replace(" · ", ", ")), count,
                        Text(choice is null ? "" : string.Join(", ", choice.PlannedOralSubjects)), review, departure];
                rows.Children.Add(new Border { Background = id == choiceSelectedId ? Brush.Parse("#E2F1EE") : index++ % 2 == 0 ? Brush.Parse("#F1F5F7") : Brushes.White, Child = CompactLine(columns, cells) });
            }
            if (shown.Length == 0) rows.Children.Add(Text("Keine Person passt zu diesen Filtern."));
        }
        void FilterChanged()
        {
            // Text events arrive deferred; ignore them once the page or the year is gone.
            if (session is null || store is null || visiblePage != "results" || !ReferenceEquals(pageHost.Content, root)) return;
            choiceFilter = new(search.Text ?? "", (classBox.SelectedItem as FilterItem)?.Value, (ChoiceStateFilter)Math.Max(0, stateBox.SelectedIndex),
                (offerBox.SelectedItem as FilterItem)?.Value, semesterBox.SelectedIndex > 0 ? semesterBox.SelectedIndex : null);
            Render();
        }
        search.TextChanged += (_, _) => FilterChanged();
        foreach (var box in new[] { classBox, stateBox, offerBox, semesterBox }) box.SelectionChanged += (_, _) => FilterChanged();
        Render();
        pageHost.Content = root; UpdateVisualNavigation();
    }

    // The working copy stays in memory until it is saved with a reason as a new teacher revision.
    private Control BuildChoiceEditor(TeacherState state, Action reload)
    {
        var record = state.Record; var id = record.Student.ExternalStudentId; var active = state.Active; var catalog = session!.Catalog;
        var now = DateTimeOffset.UtcNow;
        var original = active?.Snapshot ?? new CourseChoice(2, Guid.NewGuid(), id, record.Student.GraduationYear, catalog.Version, ChoiceValidator.Version, 1, now, now,
            SubmissionStatus.Draft, ElectionPhase.Preliminary, [], []);
        var draft = original;
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(4, 0, 8, 0) };
        var titleRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        titleRow.Children.Add(new TextBlock { Name = "ChoiceEditorTitle", Text = $"{record.Student.SyntheticDisplayName} · Klasse {record.Student.ClassName}", FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = Ink, TextWrapping = TextWrapping.Wrap });
        var master = Button("Stammdaten und Historie", () => OpenStudent(id), "Vorbildung, Ordner, frühere Stände und Wiederherstellung dieser Person.");
        master.Padding = new Thickness(8, 3); Grid.SetColumn(master, 1); titleRow.Children.Add(master);
        var close = Button("Schließen", () => { choiceSelectedId = null; reload(); }); close.Name = "ChoiceClose"; close.Padding = new Thickness(8, 3); close.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(close, 2); titleRow.Children.Add(close);
        panel.Children.Add(titleRow);
        if (active is null) panel.Children.Add(Text("Für diese Person liegt noch keine Kurswahl vor. Sie können sie hier im Namen der Beratung erfassen."));
        else panel.Children.Add(new TextBlock { Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap,
            Text = $"Aktueller Stand: {FormatRevisionSource(active.Source)} vom {active.ChangedAt.LocalDateTime:g}" + (state.RequiresReselection ? " · Nachwahl offen" + (string.IsNullOrWhiteSpace(state.Note) ? "" : " (" + state.Note + ")") : "") });

        var gridHost = new StackPanel { Spacing = 2, Name = "ChoiceGrid" };
        var feedback = new StackPanel { Spacing = 6, Name = "ChoiceFeedback" };
        var reason = new TextBox { Name = "ChoiceReason", Watermark = "Begründung der Änderung (Pflicht, steht im Änderungsprotokoll)" };
        var force = new CheckBox { Name = "ChoiceForce", Content = "Trotz Regelverletzung speichern (Sonderfall, z. B. Zwischenstand)", IsVisible = false };
        var closeFollowUp = new CheckBox { Name = "ChoiceCloseReselection", Content = "Nachwahl damit abschließen", IsChecked = true, IsVisible = false };
        Button save = null!;
        bool Changed() => !draft.Selections.OrderBy(s => s.OfferId, StringComparer.Ordinal).Select(s => s.OfferId + ":" + string.Join(",", s.Semesters.Order()))
                .SequenceEqual(original.Selections.OrderBy(s => s.OfferId, StringComparer.Ordinal).Select(s => s.OfferId + ":" + string.Join(",", s.Semesters.Order())))
            || !draft.PlannedOralSubjects.SequenceEqual(original.PlannedOralSubjects);

        void RenderFeedback()
        {
            feedback.Children.Clear();
            var validation = ChoiceOverview.Validate(draft, catalog, record);
            var summaries = ChoiceValidator.Summarize(draft, catalog);
            var total = draft.Selections.Sum(s => s.Semesters.Length);
            var totals = new TextBlock { Name = "ChoiceTotals", TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold, Foreground = total < ChoiceWorkflow.MinimumCourses ? Red : Ink,
                Text = $"{total} Halbjahreskurse (mindestens {ChoiceWorkflow.MinimumCourses}) · Kurse je HJ: {string.Join(" / ", summaries.Select(s => s.CourseCount))} · Wochenstunden je HJ: {string.Join(" / ", summaries.Select(s => s.WeeklyHours))}" };
            feedback.Children.Add(totals);
            void Box(string name, string title, ImmutableArray<string> items, IBrush background, IBrush foreground)
            {
                if (items.IsEmpty) return;
                var list = new StackPanel { Spacing = 3 };
                list.Children.Add(new TextBlock { Text = $"{title} ({items.Length})", FontWeight = FontWeight.SemiBold, Foreground = foreground });
                foreach (var item in items) list.Children.Add(new TextBlock { Text = "• " + item, TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = Ink });
                feedback.Children.Add(new Border { Name = name, Background = background, CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6), Child = list });
            }
            var violations = ChoiceOverview.Violations(validation); var hints = ChoiceOverview.Hints(validation);
            Box("ChoiceViolations", "Regelverletzungen", violations, RedSoft, Red);
            Box("ChoiceHints", "Besonderheiten – von der Oberstufenberatung zu klären", hints, YellowSoft, Yellow);
            if (violations.IsEmpty && hints.IsEmpty)
                feedback.Children.Add(new Border { Name = "ChoiceOk", Background = GreenSoft, CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6),
                    Child = new TextBlock { Text = "✓ Keine offenen Punkte: formal gültig.", Foreground = Green, FontWeight = FontWeight.SemiBold } });
            force.IsVisible = !violations.IsEmpty && Changed();
            if (violations.IsEmpty) force.IsChecked = false;
            closeFollowUp.IsVisible = state.RequiresReselection && violations.IsEmpty;
            save.IsEnabled = Changed();
        }

        void Set(CourseOffer offer, IEnumerable<int> semesters)
        {
            var list = semesters.Distinct().Order().ToImmutableArray();
            var selections = draft.Selections.Where(s => s.OfferId != offer.Id).ToImmutableArray();
            if (!list.IsEmpty) selections = selections.Add(new(offer.Id, offer.SubjectId, offer.Type, offer.Variant, list));
            var oral = draft.PlannedOralSubjects.Where(o => selections.Any(s => s.SubjectId == o && s.Type != CourseType.Lf)).ToImmutableArray();
            draft = draft with { Selections = selections, PlannedOralSubjects = oral };
            RenderGrid();
        }

        void RenderGrid()
        {
            gridHost.Children.Clear();
            const string columns = "*,46,46,46,46,34";
            var head = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), Margin = new Thickness(0, 0, 0, 2) };
            for (var h = 1; h <= 4; h++) { var t = new TextBlock { Text = "HJ " + h, FontSize = 12, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center }; Grid.SetColumn(t, h); head.Children.Add(t); }
            gridHost.Children.Add(head);
            foreach (var group in new[] { (Title: "Leistungsfächer", Lf: true), (Title: "Basis-, Wahl- und Seminarkurse", Lf: false) })
            {
                gridHost.Children.Add(new TextBlock { Text = group.Title, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Accent, Margin = new Thickness(0, 6, 0, 0) });
                var selections = draft.Selections.Where(s => (s.Type == CourseType.Lf) == group.Lf)
                    .OrderBy(s => s.Type).ThenBy(s => SubjectName(s.SubjectId), StringComparer.CurrentCulture).ToArray();
                if (selections.Length == 0) gridHost.Children.Add(new TextBlock { Text = "– noch nichts gewählt –", Foreground = Muted, FontSize = 12 });
                foreach (var selection in selections)
                {
                    var offer = catalog.Offers.FirstOrDefault(o => o.Id == selection.OfferId);
                    var key = selection.OfferId.Replace('-', '_');
                    var row = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), MinHeight = 30 };
                    var offered = offer?.Status is OfferStatus.Offered or OfferStatus.Established;
                    var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = offered ? Ink : Red,
                        Text = (offer is null ? selection.OfferId + " (unbekanntes Angebot)" : OfferLabel(offer.Id) + (offered ? "" : $" ({FormatOfferStatus(offer.Status)})"))
                            + (draft.PlannedOralSubjects.Contains(selection.SubjectId) && selection.Type != CourseType.Lf ? "  · mündlich" : "") };
                    row.Children.Add(label);
                    for (var h = 1; h <= 4; h++)
                    {
                        var semester = h;
                        var scheduled = offer?.Schedule.Any(s => s.Semester == semester) == true;
                        var box = new CheckBox { Name = $"Choice_{key}_{semester}", IsChecked = selection.Semesters.Contains(semester), IsEnabled = offer is not null && (scheduled || selection.Semesters.Contains(semester)),
                            HorizontalAlignment = HorizontalAlignment.Center, MinWidth = 0, Padding = new Thickness(0) };
                        ToolTip.SetTip(box, scheduled ? (offer!.Type is CourseType.Lf or CourseType.Seminar ? "Wird immer für alle angebotenen Halbjahre belegt." : "Halbjahr " + semester + " belegen oder abwählen.") : "In diesem Halbjahr nicht angeboten.");
                        box.IsCheckedChanged += (_, _) =>
                        {
                            if (offer is null) return;
                            if (offer.Type is CourseType.Lf or CourseType.Seminar) Set(offer, box.IsChecked == true ? offer.Schedule.Select(s => s.Semester) : []);
                            else Set(offer, box.IsChecked == true ? selection.Semesters.Add(semester) : selection.Semesters.Remove(semester));
                        };
                        Grid.SetColumn(box, h); row.Children.Add(box);
                    }
                    var remove = new Button { Name = "ChoiceRemove_" + key, Content = "✕", Padding = new Thickness(6, 1), MinHeight = 24, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center };
                    ToolTip.SetTip(remove, "Diesen Kurs vollständig aus der Wahl entfernen.");
                    remove.Click += (_, _) => { draft = draft with { Selections = [.. draft.Selections.Where(s => s.OfferId != selection.OfferId)],
                        PlannedOralSubjects = [.. draft.PlannedOralSubjects.Where(o => o != selection.SubjectId || draft.Selections.Any(s => s.SubjectId == o && s.OfferId != selection.OfferId && s.Type != CourseType.Lf))] }; RenderGrid(); };
                    Grid.SetColumn(remove, 5); row.Children.Add(remove);
                    gridHost.Children.Add(new Border { BorderBrush = Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = row });
                }
            }
            var profile = new ProfileContext(ProfileStatus.Verified, record.Student.Prerequisites);
            var addItems = catalog.Offers.Where(o => o.Status is OfferStatus.Offered or OfferStatus.Established && !draft.Selections.Any(s => s.OfferId == o.Id))
                .OrderBy(o => o.Type == CourseType.Lf ? 0 : 1).ThenBy(o => SubjectName(o.SubjectId), StringComparer.CurrentCulture).ThenBy(o => o.Type)
                .Select(o => new FilterItem(o.Id, OfferLabel(o.Id) + (new ChoiceValidator().GetAvailability(o, profile).CanSelect ? "" : "  (persönliche Voraussetzung fehlt/unklar)"))).ToArray();
            var add = new ComboBox { Name = "ChoiceAdd", ItemsSource = addItems, PlaceholderText = "Kurs hinzufügen …", MinWidth = 300, Margin = new Thickness(0, 8, 0, 0) };
            add.SelectionChanged += (_, _) =>
            {
                if ((add.SelectedItem as FilterItem)?.Value is not { } offerId) return;
                var offer = catalog.Offers.Single(o => o.Id == offerId);
                // Choosing a subject as LF replaces its basic course, as in the pupil program.
                if (offer.Type == CourseType.Lf) draft = draft with { Selections = [.. draft.Selections.Where(s => !(s.SubjectId == offer.SubjectId && s.Type != CourseType.Lf))],
                    PlannedOralSubjects = draft.PlannedOralSubjects.Remove(offer.SubjectId) };
                var required = ChoiceWorkflow.Required(draft with { PlannedOralSubjects = [] }, profile).FirstOrDefault(r => r.SubjectId == offer.SubjectId);
                Set(offer, offer.Type == CourseType.Bf && required is not null && offer.SubjectId is "GK" or "GEO" ? required.Semesters : offer.Schedule.Select(s => s.Semester));
            };
            gridHost.Children.Add(add);
            var oralSubjects = draft.Selections.Where(s => s.Type == CourseType.Bf).Select(s => s.SubjectId).Distinct().OrderBy(SubjectName, StringComparer.CurrentCulture).ToArray();
            var oralRow = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            oralRow.Children.Add(new TextBlock { Text = "Mündlich geplant:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            var oralBoxes = new List<ComboBox>();
            for (var i = 0; i < 2; i++)
            {
                var current = draft.PlannedOralSubjects.ElementAtOrDefault(i);
                var items = new[] { new FilterItem(null, "— nicht geplant —") }.Concat(oralSubjects.Select(s => new FilterItem(s, SubjectName(s)))).ToArray();
                var box = new ComboBox { Name = "ChoiceOral" + (i + 1), ItemsSource = items, SelectedItem = items.FirstOrDefault(x => x.Value == current) ?? items[0], MinWidth = 170, Margin = new Thickness(0, 0, 8, 0) };
                oralBoxes.Add(box); oralRow.Children.Add(box);
            }
            foreach (var box in oralBoxes) box.SelectionChanged += (_, _) =>
            {
                draft = draft with { PlannedOralSubjects = [.. oralBoxes.Select(b => (b.SelectedItem as FilterItem)?.Value).OfType<string>().Distinct()] };
                RenderGrid();
            };
            ToolTip.SetTip(oralRow, "Unverbindliche Planung der zwei mündlichen Prüfungsfächer. Zur Auswahl stehen die belegten Basisfächer.");
            gridHost.Children.Add(oralRow);
            RenderFeedback();
        }

        save = Button("Änderung speichern", () =>
        {
            var validation = ChoiceOverview.Validate(draft, catalog, record);
            if (validation.Status == ReviewStatus.Invalid && force.IsChecked != true)
                throw new InvalidOperationException("Bitte die Regelverletzungen beheben oder „Trotz Regelverletzung speichern“ ankreuzen.");
            var text = reason.Text ?? "";
            session.Save(draft with { OfferVersion = catalog.Version, RuleVersion = ChoiceValidator.Version, Revision = active is null ? 1 : active.Snapshot.Revision + 1, UpdatedAt = DateTimeOffset.UtcNow },
                active?.Id, RevisionSource.TeacherEdit, text);
            var closed = false;
            if (state.RequiresReselection && closeFollowUp.IsChecked == true && validation.Status != ReviewStatus.Invalid) { session.FollowUp(id, false, null, text); closed = true; }
            Refresh(preserveEditor: true);
            status.Text = record.Student.SyntheticDisplayName + ": Kurswahl als neuer Stand gespeichert" + (closed ? "; Nachwahl abgeschlossen." : ".")
                + (validation.Status == ReviewStatus.Invalid ? " Es bestehen weiterhin Regelverletzungen." : "");
            reload();
        }, "Speichert die geänderte Wahl als neuen Stand mit Begründung. Die ursprüngliche Schülerwahl bleibt in der Historie.");
        save.Name = "ChoiceSave";
        var print = Button("Kontrollbogen (PDF) …", () =>
        {
            if (Changed()) throw new InvalidOperationException("Bitte zuerst die Änderung speichern; gedruckt wird der gespeicherte Stand.");
            return SavePdf(ControlSheetsPdf([state]), "Kontrollbogen-" + id);
        }, "Kontrollbogen dieser Person mit Unterschriftenzeile.");
        print.Name = "ChoicePrint";
        var reset = Button("Verwerfen", () => { draft = original; reason.Text = ""; RenderGrid(); }, "Ungespeicherte Änderungen zurücknehmen.");
        reset.Name = "ChoiceReset";
        RenderGrid();
        // Warnings first: red and yellow must be visible without scrolling.
        panel.Children.Add(feedback); panel.Children.Add(gridHost);
        panel.Children.Add(reason); panel.Children.Add(force); panel.Children.Add(closeFollowUp);
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var button in new[] { Primary(save), reset, print }) { button.Margin = new Thickness(0, 0, 8, 6); buttons.Children.Add(button); }
        panel.Children.Add(buttons);
        return new Border { BorderBrush = Line, BorderThickness = new Thickness(1, 0, 0, 0), Padding = new Thickness(12, 0, 0, 0), Child = new ScrollViewer { Content = panel } };
    }

    private string FilterDescription()
    {
        var parts = new List<string>();
        if (choiceFilter.ClassName is { } c) parts.Add("Klasse " + c);
        if (choiceFilter.State != ChoiceStateFilter.All) parts.Add(choiceFilter.State switch
        {
            ChoiceStateFilter.WithoutChoice => "ohne Wahl", ChoiceStateFilter.Incomplete => "unvollständige Kurswahl", ChoiceStateFilter.Violation => "Regelverletzung",
            ChoiceStateFilter.NeedsReview => "zu prüfen", ChoiceStateFilter.Valid => "formal gültig", ChoiceStateFilter.ReselectionOpen => "Nachwahl offen", _ => "Feststellungsprüfung nötig"
        });
        if (choiceFilter.OfferId is { } o && session!.Catalog.Offers.FirstOrDefault(x => x.Id == o) is { } offer) parts.Add(ChoiceOverview.OfferTitle(session.Catalog, offer));
        if (choiceFilter.Semester is { } h) parts.Add("HJ " + h);
        if (!string.IsNullOrWhiteSpace(choiceFilter.Text)) parts.Add("Suche „" + choiceFilter.Text.Trim() + "“");
        return "Abitur " + session!.Catalog.GraduationYear + " · " + (parts.Count == 0 ? "alle Personen" : string.Join(" · ", parts));
    }

    internal byte[] OverviewPdf(IReadOnlyCollection<TeacherState> states) => PlanningPrint.Overview(session!.Catalog.GraduationYear, FilterDescription(),
        [.. states.Select(s => new[] { s.Record.Student.SyntheticDisplayName, s.Record.Student.ClassName,
            s.Active is null ? "–" : PlanningPresentation.Choices(s.Active.Snapshot, true, session!.Catalog), s.Active is null ? "" : PlanningPresentation.Choices(s.Active.Snapshot, false, session!.Catalog).Replace(" · ", ", "),
            s.Active is null ? "" : string.Join(", ", s.Active.Snapshot.PlannedOralSubjects),
            FormatStatus(s.Validation?.Status) + (s.RequiresReselection ? ", Nachwahl" : "") })]);

    internal byte[] ControlSheetsPdf(IReadOnlyCollection<TeacherState> states) => PlanningPrint.ControlSheets(
        states.Select(s => new ControlSheetItem(s.Record, s.Active?.Snapshot, [.. ChoiceOverview.Violations(s.Validation)], [.. ChoiceOverview.Hints(s.Validation)])), session!.Catalog);

    internal byte[] CourseListsPdf(IReadOnlyCollection<TeacherState> states)
    {
        var lists = choiceFilter.OfferId is { } offerId
            ? [ChoiceOverview.Members(states, session!.Catalog, offerId, choiceFilter.Semester)]
            : ChoiceOverview.AllCourseLists(states, session!.Catalog);
        return PlanningPrint.CourseLists(session.Catalog.GraduationYear, lists.Where(l => !l.Members.IsEmpty).Select(l => new CourseListPrint(
            l.Title + (choiceFilter.ClassName is { } c ? " · Klasse " + c : ""),
            [.. l.Members.Select((m, i) => new[] { (i + 1).ToString(), m.Student.Student.SyntheticDisplayName, m.Student.Student.ClassName,
                m.Semesters.SequenceEqual(new[] { 1, 2, 3, 4 }) ? "HJ 1–4" : "HJ " + string.Join(", ", m.Semesters) })])));
    }

    internal byte[] ReselectionsPdf(IReadOnlyCollection<TeacherState> states)
    {
        var catalog = session!.Catalog;
        var items = states.Where(s => s.RequiresReselection && s.Active is not null).Select(s =>
        {
            var removed = s.Active!.Source == RevisionSource.OfferCancellation
                ? s.Active.Changes.Where(c => c.Before.Length > 0 && c.After.Length == 0 && catalog.Offers.Any(o => o.Id == c.OfferId)).Select(c => c.OfferId).ToArray() : [];
            return new ReselectionPrintItem(s.Record, s.Active.Snapshot, removed) { Notes = [.. ChoiceOverview.Violations(s.Validation)] };
        }).ToArray();
        if (items.Length == 0) throw new InvalidOperationException("Unter den angezeigten Personen ist keine Nachwahl offen.");
        return PlanningPrint.Reselections(items, catalog);
    }

    private async Task SavePdf(byte[] pdf, string name)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "PDF speichern und zum Drucken öffnen", DefaultExtension = "pdf",
            SuggestedFileName = $"{name}-Abitur-{session!.Catalog.GraduationYear}-{DateTime.Now:yyyyMMdd-HHmm}.pdf", FileTypeChoices = [new("PDF") { Patterns = ["*.pdf"] }] });
        var path = file?.TryGetLocalPath(); if (path is null) return;
        if (!Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Dateiendung muss .pdf sein.");
        WriteNew(path, pdf);
        status.Text = "PDF gespeichert: " + path + ". Im PDF-Programm drucken (Strg+P). Enthält personenbezogene Daten – nur intern verwenden.";
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { status.Text += " Automatisches Öffnen nicht möglich: " + ex.Message; }
    }
}
