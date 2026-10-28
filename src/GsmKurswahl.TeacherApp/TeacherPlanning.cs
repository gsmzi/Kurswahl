using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.ImportExport;

namespace GsmKurswahl.TeacherApp;

public sealed partial class TeacherWindow
{
    private bool planningIncludeExpected = true;
    private PlanningDocument? renderedPlanningDocument;
    private void ShowPlanning()
    {
        Ready(); visiblePage = "planning";
        var panel = new StackPanel { Spacing = 6 };
        pageHost.Content = Scroll(panel); UpdateVisualNavigation();
        var planning = (IPlanningStore)store!;
        if (!planning.PlanningEnabled)
        {
            panel.Children.Add(Text("Die Kursplanung ergänzt den lokalen Lehrerbestand um unveränderliche Planungsstände und Entscheidungen (Datenbankschema 1 → 2). Bestehende Daten bleiben erhalten. Ältere Programmversionen können Schema 2 nicht öffnen."));
            panel.Children.Add(Primary(Button("Kursplanung aktivieren · Datenbankschema 2", () => { planning.EnablePlanning(); ShowPlanning(); })));
            return;
        }
        var parameters = new StackPanel { Spacing = 8 };

        var analysisPanel = new StackPanel { Spacing = 10 };
        var historyPanel = new StackPanel { Spacing = 10 };
        var changesPanel = new StackPanel { Spacing = 10 };
        var tabs = new TabControl { Name = "PlanningTabs", ItemsSource = new[] {
            Tab("Auswertung", analysisPanel), Tab("Gespeicherte Stände", historyPanel), Tab("Angebot ändern", changesPanel) }, SelectedIndex = 0 };

        var include = new ComboBox { ItemsSource = new[] { "Mit voraussichtlichen Abgängern", "Ohne voraussichtliche Abgänger" }, SelectedIndex = planningIncludeExpected ? 0 : 1, Name = "PlanningIncludeDepartures", MinWidth = 310 };
        var name = new TextBox { Text = "Planung " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Watermark = "Name des Planungsstands", Name = "PlanningName" };
        parameters.Children.Add(Text("Name des zu speichernden Planungsstands")); parameters.Children.Add(name);
        historyPanel.Children.Add(new Expander { Header = "Name des Planungsstands", Content = parameters, IsExpanded = true });
        var report = new StackPanel { Spacing = 6, Name = "PlanningReport" };
        PlanningDocument Current() => CoursePlanning.Capture(store!, session!.Catalog, Actor, name.Text ?? "", include.SelectedIndex == 0);
        PlanningDocument? displayed = null;
        var sourceLabel = new TextBlock { Text = "Aktueller Bestand", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        toolbar.Children.Add(include);
        var currentButton = Button("Aktuell", () => { displayed = null; sourceLabel.Text = "Aktueller Bestand"; RenderPlanningReport(report, Current(), 24); });
        currentButton.Padding = new Thickness(10, 4); currentButton.MinHeight = 32; toolbar.Children.Add(currentButton); toolbar.Children.Add(sourceLabel);
        toolbar.Children.Add(Button("Drucken …", () => PrintPlanning(renderedPlanningDocument ?? throw new InvalidOperationException("Zuerst Wahlzahlen anzeigen."))));
        ExportTable DemandExport()
        {
            var d = renderedPlanningDocument ?? throw new InvalidOperationException("Zuerst Wahlzahlen anzeigen.");
            return new(["Stand", "Zeitpunkt", "Szenario", "Voraussichtliche Abgänger einbeziehen", "Fach", "LF", "LF bili", "BF", "BF bili", "Wahl"],
                [.. PlanningPresentation.DemandTable(d).Select(r => new[] { d.Name, d.CreatedAt.ToString("O"), d.IsScenario.ToString(), d.IncludeExpectedDepartures.ToString() }.Concat(r).ToArray())]);
        }
        analysisPanel.Children.Add(Button("Angezeigte Wahlzahlen als CSV …", () => ExportTableFile(DemandExport(), false, "Wahlzahlen")));
        analysisPanel.Children.Add(Button("Angezeigte Wahlzahlen als XLSX …", () => ExportTableFile(DemandExport(), true, "Wahlzahlen")));
        panel.Children.Add(toolbar); panel.Children.Add(tabs);
        include.SelectionChanged += (_, _) => {
            planningIncludeExpected = include.SelectedIndex == 0;
            var document = (displayed ?? Current()) with { IncludeExpectedDepartures = planningIncludeExpected };
            RenderPlanningReport(report, document, 24);
        };
        historyPanel.Children.Add(Button("Planungsstand unveränderlich speichern", () => { planning.SavePlanningDocument(Current()); status.Text = "Planungsstand gespeichert. Aktive Wahlen unverändert."; ShowPlanning(); }));
        var savedItems = planning.PlanningDocuments().Select(d => new PlanningItem(d)).Reverse().ToArray();
        var saved = new ComboBox { ItemsSource = savedItems, PlaceholderText = "Gespeicherten Planungsstand wählen", Name = "PlanningSaved" };
        var compareTarget = new ComboBox { ItemsSource = new object[] { "Aktueller Bestand" }.Concat(savedItems).ToArray(), SelectedIndex = 0, Name = "PlanningCompareTarget" };
        historyPanel.Children.Add(saved);
        historyPanel.Children.Add(Text("Vergleichsziel (aktueller Bestand oder zweiter gespeicherter Stand / Szenario)"));
        historyPanel.Children.Add(compareTarget);
        historyPanel.Children.Add(Button("Gespeicherten Stand anzeigen und mit aktuell vergleichen", () =>
        {
            var previous = (saved.SelectedItem as PlanningItem)?.Document ?? throw new ArgumentException("Planungsstand auswählen.");
            var second = (compareTarget.SelectedItem as PlanningItem)?.Document;
            displayed = previous; sourceLabel.Text = previous.Name;
            include.SelectedIndex = previous.IncludeExpectedDepartures ? 0 : 1;
            RenderPlanningReport(report, previous, previous.TargetGroupSize); tabs.SelectedIndex = 0;
            var targetDoc = (second ?? Current()) with { IncludeExpectedDepartures = previous.IncludeExpectedDepartures };
            var targetName = second is null ? "aktuell" : second.Name;
            var before = CoursePlanning.Analyze(previous, previous.TargetGroupSize); var after = CoursePlanning.Analyze(targetDoc, previous.TargetGroupSize);
            report.Children.Add(Text($"Vergleich mit {targetName} · gleicher Abgängerfilter wie gespeichert"));
            foreach (var id in before.Demand.Select(d => d.OfferId).Union(after.Demand.Select(d => d.OfferId)).Order())
            for (var h = 1; h <= 4; h++)
            {
                var a = before.Demand.SingleOrDefault(d => d.OfferId == id && d.Semester == h)?.Relevant ?? 0;
                var b = after.Demand.SingleOrDefault(d => d.OfferId == id && d.Semester == h)?.Relevant ?? 0;
                if (a != b) report.Children.Add(Text($"{id} · HJ {h}: {a} → {b} ({b-a:+0;-0;0})"));
            }
            foreach (var combo in before.Combinations.Select(c => c.Combination).Union(after.Combinations.Select(c => c.Combination)).Order())
            {
                var a = before.Combinations.SingleOrDefault(c => c.Combination == combo)?.Relevant ?? 0;
                var b = after.Combinations.SingleOrDefault(c => c.Combination == combo)?.Relevant ?? 0;
                if (a != b) report.Children.Add(Text($"LF-Kombination {combo}: {a} → {b} ({b-a:+0;-0;0})"));
            }
            report.Children.Add(Text($"Personen: {before.All} → {after.All}; planungsrelevant: {before.Relevant} → {after.Relevant}. Gespeicherter Regelstand: {previous.RuleVersion}."));
        }));
        var offers = new WrapPanel { Orientation = Orientation.Horizontal };
        var selection = session!.Catalog.Offers.Where(o => (o.Status is OfferStatus.Offered or OfferStatus.Established) && PlanningPresentation.CanCancel(o))
            .OrderBy(o => o.SubjectId).ThenBy(o => o.Type)
            .Select(o => (Offer: o, Box: new CheckBox { Content = $"{o.SubjectId} · {o.Type.ToString().ToUpperInvariant()}" + (o.Variant == CourseVariant.Bilingual ? " bilingual" : ""), Name = "PlanningCancel_" + o.Id.Replace('-', '_') })).ToArray();
        foreach (var row in selection) { row.Box.Width = 260; row.Box.Margin = new Thickness(0, 0, 8, 2); offers.Children.Add(row.Box); }
        changesPanel.Children.Add(new Expander { Header = "Angebotsvarianten zur Streichung wählen", Content = offers });
        var impact = new StackPanel { Spacing = 2, Name = "PlanningImpact" };
        var confirmation = new CheckBox { Content = "Diese Vorschau geprüft; Angebotsänderung und neue Verwaltungsrevisionen ausdrücklich übernehmen", Name = "PlanningConfirm" };
        CancellationPreview? preview = null;
        foreach (var item in selection) item.Box.IsCheckedChanged += (_, _) => { preview = null; confirmation.IsChecked = false; impact.Children.Clear(); };
        void RenderImpact()
        {
            if (preview is null) return;
            confirmation.IsChecked = false; impact.Children.Clear();
            foreach (var offer in preview.Catalog.Offers)
            {
                var before = preview.Basis.Catalog.Offers.Single(o => o.Id == offer.Id);
                if (before.Status != offer.Status) impact.Children.Add(Text(offer.Status == OfferStatus.Cancelled ? "Entfällt: " + offer.Id : "Angebot geändert: " + offer.Id));
            }
            var affected = preview.Changes.Where(c => !c.RemovedOffers.IsEmpty).ToArray();
            impact.Children.Add(Text($"{affected.Length} betroffene Schüler · Vorschau, noch keine Änderung gespeichert"));
            const string columns = "180,55,150,190,*";
            impact.Children.Add(CompactLine(columns, [Text("Schüler"), Text("Klasse"), Text("Entfällt"), Text("Verbleibende LF"), Text("Verbleibende BF / Wahlfächer")]));
            var rowIndex = 0;
            foreach (var change in affected)
            {
                var student = preview.Basis.Students.Single(s => s.Student.ExternalStudentId == change.StudentId);
                var row = CompactLine(columns, [Text(student.Student.SyntheticDisplayName), Text(student.Student.ClassName),
                    Text(string.Join(", ", change.RemovedOffers)), Text(PlanningPresentation.Choices(change.Choice, true)), Text(PlanningPresentation.Choices(change.Choice, false))]);
                ToolTip.SetTip(row, string.Join("\n", change.Validation.Messages.Select(m => m.Explanation)));
                impact.Children.Add(new Border { Child = row, Background = rowIndex++ % 2 == 0 ? Brush.Parse("#F1F5F7") : Brushes.White });
            }
            if (preview.Restoring) impact.Children.Add(Text("Die Wiederherstellung betrifft das Angebot. Entfernte Schülerwünsche werden nicht automatisch eingetragen; eine Wahl kann anschließend im Schülerbestand aus ihrer Historie wiederhergestellt werden."));
        }
        var simulationActions = new WrapPanel { Orientation = Orientation.Horizontal };
        changesPanel.Children.Add(simulationActions);
        simulationActions.Children.Add(Button("Streichung nur simulieren", () =>
        {
            preview = CoursePlanning.PreviewCancellation(Current(), selection.Where(s => s.Box.IsChecked == true).Select(s => s.Offer.Id));
            RenderImpact();
        }));
        historyPanel.Children.Add(Button("Angebot des gespeicherten Stands als Wiederherstellung simulieren", () =>
        {
            var previous = (saved.SelectedItem as PlanningItem)?.Document ?? throw new ArgumentException("Planungsstand auswählen.");
            preview = CoursePlanning.PreviewOfferRestoration(Current(), previous); RenderImpact(); tabs.SelectedIndex = 2;
        }));
        simulationActions.Children.Add(Button("Vorschau als separates Szenario speichern", () =>
        {
            if (preview is null) throw new InvalidOperationException("Zuerst simulieren.");
            if (!planning.PlanningDocuments().Any(d => d.Id == preview.Basis.Id)) planning.SavePlanningDocument(preview.Basis);
            planning.SavePlanningDocument(CoursePlanning.Scenario(preview, name.Text ?? ""));
            status.Text = "Szenario gespeichert; aktive Wahlen unverändert."; ShowPlanning();
        }));
        changesPanel.Children.Add(impact);
        changesPanel.Children.Add(confirmation);
        changesPanel.Children.Add(Primary(Button("Geprüfte Angebotsänderung im Lehrerbestand übernehmen", () =>
        {
            if (preview is null) throw new InvalidOperationException("Zuerst eine Vorschau erstellen.");
            CoursePlanning.Confirm(planning, preview, Actor, "", confirmation.IsChecked == true);
            session = new TeacherSession(store!, preview.Catalog, Actor);
            var hasRemoved = preview.Changes.Any(c => !c.RemovedOffers.IsEmpty);
            var committedCatalog = preview.Catalog; preview = null; confirmation.IsChecked = false;
            workspace!.Update(workspace.Active! with { Catalog = committedCatalog });
            Refresh(preserveEditor: true); ShowPlanning();
            status.Text = "Angebotsänderung gespeichert. Aktuelles Fachangebot erneut verteilen.";
            if (hasRemoved)
            {
                try { status.Text += " Nachwahlbögen erstellt: " + CreateReselectionSheets(); }
                catch (Exception ex) { status.Text = "Streichung ist gespeichert. Nachwahlbögen konnten nicht vollständig erstellt werden: " + ex.Message + ". Über Nachwahlbögen erneut erstellen wiederholen."; }
            }
        })));
        changesPanel.Children.Add(Button("Nachwahlbögen erneut erstellen", () => { var folder = CreateReselectionSheets(); status.Text = "Nachwahlbögen: " + folder; }));
        changesPanel.Children.Add(Button("Nachwahlbögen öffnen", OpenReselectionFolder));
        analysisPanel.Children.Add(report);
        RenderPlanningReport(report, Current(), 24);
        var wide = Width >= 1150;
        void UpdatePlanningWidth(object? sender, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Property != WidthProperty) return;
            if (wide == (Width >= 1150)) return;
            wide = Width >= 1150;
            RenderPlanningReport(report, (displayed ?? Current()) with { IncludeExpectedDepartures = include.SelectedIndex == 0 }, 24);
        }
        PropertyChanged += UpdatePlanningWidth;
        panel.DetachedFromVisualTree += (_, _) => PropertyChanged -= UpdatePlanningWidth;
    }
    private sealed record PlanningItem(PlanningDocument Document)
    {
        public override string ToString() => (Document.IsScenario ? "Szenario · " : "Bestand · ") + Document.Name + " · " + Document.CreatedAt.LocalDateTime.ToString("g");
    }
    private void RenderPlanningReport(StackPanel panel, PlanningDocument document, int groupSize)
    {
        renderedPlanningDocument = document;
        panel.Children.Clear(); var analysis = CoursePlanning.Analyze(document, groupSize);
        panel.Children.Add(Text($"{analysis.Relevant} planungsrelevant aus {analysis.All} Personen · {analysis.ExpectedDepartures} voraussichtliche / {analysis.ConfirmedDepartures} bestätigte Abgänger · {analysis.WithoutChoice} ohne Wahl"));
        var legend = new TextBlock { Text = "Zahl = durchgehend · KS1/2 bei Abweichungen · 1:… bis 4:… = Halbjahre · bestätigte Abgänger ausgeschlossen", TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Muted };
        panel.Children.Add(legend);
        const string columns = "1.8*,.55*,.7*,1.25*,1*,1.1*";
        var subjects = document.Catalog.Subjects.OrderBy(s => s.Name, StringComparer.CurrentCulture).ToArray();
        var split = Width >= 1150;
        var matrix = new Grid { Name = "PlanningMatrix", ColumnDefinitions = new ColumnDefinitions(split ? "*,16,*" : "*") };
        var blocks = new[] { new StackPanel(), new StackPanel() };
        for (var b = 0; b < (split ? 2 : 1); b++)
        {
            var header = CompactLine(columns, [Text("Fach"), Text("LF"), Text("LF bili"), Text("BF"), Text("BF bili"), Text("Wahl")]);
            blocks[b].Children.Add(new Border { Background = SoftAccent, Child = header });
            Grid.SetColumn(blocks[b], b * 2); matrix.Children.Add(blocks[b]);
        }
        var index = 0;
        foreach (var subject in subjects)
        {
            Control Cell(CourseType type, CourseVariant variant, bool extra = false)
            {
                var offers = document.Catalog.Offers.Where(o => o.SubjectId == subject.Id && o.Variant == variant
                    && (extra ? o.Type is CourseType.Elective or CourseType.Seminar : o.Type == type)).ToArray();
                var content = new StackPanel { Spacing = 0 };
                if (offers.Length == 0) content.Children.Add(Text("–"));
                foreach (var offer in offers)
                {
                    var scheduled = Enumerable.Range(1, 4).Select(h => offer.Schedule.Any(s => s.Semester == h)
                        ? (int?)analysis.Demand.Single(d => d.OfferId == offer.Id && d.Semester == h).Relevant : null).ToArray();
                    var value = offer.Status is OfferStatus.Offered or OfferStatus.Established ? PlanningDisplay.PeriodCounts(scheduled, type == CourseType.Bf && subject.Id is "GK" or "GEO")
                        : offer.Status == OfferStatus.Cancelled ? "entfällt" : "geplant";
                    var text = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, Name = "Demand_" + offer.Id.Replace('-', '_'), FontSize = 13,
                        FontWeight = FontWeight.SemiBold, Foreground = scheduled.Any(n => n > 0) ? Accent : Muted };
                    ToolTip.SetTip(text, offer.Id + " · " + offer.Status + "\n" + string.Join("\n", analysis.Demand.Where(d => d.OfferId == offer.Id)
                        .Select(d => $"HJ {d.Semester}: {d.Relevant} verbleibend / {d.All} insgesamt")));
                    content.Children.Add(text);
                }
                return content;
            }
            var label = Text(subject.Id + " · " + subject.Name);
            var row = CompactLine(columns, [label, Cell(CourseType.Lf, CourseVariant.Normal), Cell(CourseType.Lf, CourseVariant.Bilingual),
                Cell(CourseType.Bf, CourseVariant.Normal), Cell(CourseType.Bf, CourseVariant.Bilingual), Cell(CourseType.Elective, CourseVariant.Normal, true)]);
            blocks[split && index >= (subjects.Length + 1) / 2 ? 1 : 0].Children.Add(new Border { Name = "PlanningSubject_" + subject.Id,
                Background = index++ % 2 == 0 ? Brush.Parse("#F1F5F7") : Brushes.White, Child = row });
        }
        panel.Children.Add(matrix);
        var combinations = new StackPanel { Spacing = 2 };
        foreach (var c in analysis.Combinations) combinations.Children.Add(Text($"{c.Combination}: {c.Relevant}"));
        panel.Children.Add(new Expander { Header = "LF-Kombinationen", Content = combinations });
        var overlaps = new StackPanel { Spacing = 2 };
        foreach (var group in analysis.Overlaps.GroupBy(o => (o.FirstOffer, o.SecondOffer)))
        {
            int?[] counts = [.. Enumerable.Range(1, 4).Select(h => (int?)(group.SingleOrDefault(o => o.Semester == h)?.Relevant ?? 0))];
            overlaps.Children.Add(Text($"{group.Key.FirstOffer} × {group.Key.SecondOffer}: {PlanningDisplay.PeriodCounts(counts)}"));
        }
        panel.Children.Add(new Expander { Header = "Gemeinsame Belegungen / Überschneidungen", Content = overlaps });
    }
}
