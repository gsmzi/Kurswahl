using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;

namespace GsmKurswahl.TeacherApp;

public sealed partial class TeacherWindow
{
    private bool planningIncludeExpected = true;
    private int planningGroupSize = 24;
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
        var size = new NumericUpDown { Minimum = 1, Maximum = 100, Value = planningGroupSize, FormatString = "0", Width = 110, Name = "PlanningGroupSize" };
        ToolTip.SetTip(size, "Zielgröße für die aufgerundete Gruppenschätzung im Tooltip (keine Unterrichtsgruppen oder Schienen).");
        var name = new TextBox { Text = "Planung " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Watermark = "Name des Planungsstands", Name = "PlanningName" };
        parameters.Children.Add(Text("Name des zu speichernden Planungsstands")); parameters.Children.Add(name);
        historyPanel.Children.Add(new Expander { Header = "Name und Gruppenschätzung", Content = parameters, IsExpanded = true });
        var report = new StackPanel { Spacing = 6, Name = "PlanningReport" };
        PlanningDocument Current() => CoursePlanning.Capture(store!, session!.Catalog, Actor, name.Text ?? "", include.SelectedIndex == 0) with { TargetGroupSize = planningGroupSize };
        PlanningDocument? displayed = null;
        var sourceLabel = new TextBlock { Text = "Aktueller Bestand", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        toolbar.Children.Add(include);
        toolbar.Children.Add(new TextBlock { Text = "Gruppengröße ≈", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
        toolbar.Children.Add(size);
        var currentButton = Button("Aktuell", () => { displayed = null; sourceLabel.Text = "Aktueller Bestand"; RenderPlanningReport(report, Current(), planningGroupSize); });
        currentButton.Padding = new Thickness(10, 4); currentButton.MinHeight = 32; toolbar.Children.Add(currentButton); toolbar.Children.Add(sourceLabel);
        panel.Children.Add(toolbar); panel.Children.Add(tabs);
        include.SelectionChanged += (_, _) => {
            planningIncludeExpected = include.SelectedIndex == 0;
            var document = (displayed ?? Current()) with { IncludeExpectedDepartures = planningIncludeExpected };
            RenderPlanningReport(report, document, planningGroupSize);
        };
        size.ValueChanged += (_, _) => {
            planningGroupSize = Math.Clamp((int)(size.Value ?? 24), 1, 100);
            var document = (displayed ?? Current()) with { IncludeExpectedDepartures = include.SelectedIndex == 0, TargetGroupSize = planningGroupSize };
            RenderPlanningReport(report, document, planningGroupSize);
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
            planningGroupSize = previous.TargetGroupSize; size.Value = previous.TargetGroupSize;
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
        var selection = session!.Catalog.Offers.Where(o => o.Status is OfferStatus.Offered or OfferStatus.Established)
            .Select(o => (Offer: o, Box: new CheckBox { Content = $"{o.SubjectId} · {o.Type} · {o.Variant} · {o.Id}", Name = "PlanningCancel_" + o.Id.Replace('-', '_') })).ToArray();
        foreach (var row in selection) { row.Box.Width = 260; row.Box.Margin = new Thickness(0, 0, 8, 2); offers.Children.Add(row.Box); }
        changesPanel.Children.Add(new Expander { Header = "Angebotsvarianten zur Streichung wählen", Content = offers });
        var impactFilterPanel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4) };
        var impactOutcomeFilter = new ComboBox { Name = "PlanningImpactOutcomeFilter", ItemsSource = new[] { "Alle Folgen", "nachwahl_nötig", "nach_streichung_weiterhin_gültig", "ungeprüfter_sonderfall", "ungültig" }, SelectedIndex = 0, MinWidth = 240, Margin = new Thickness(0, 0, 8, 4) };
        var impactDepartureFilter = new ComboBox { Name = "PlanningImpactDepartureFilter", ItemsSource = new[] { "Alle Personen", "Nur aktive / planungsrelevante", "Nur Abgänger (abgänger)" }, SelectedIndex = 0, MinWidth = 220, Margin = new Thickness(0, 0, 8, 4) };
        var impactAffectedOnly = new CheckBox { Name = "PlanningImpactOnlyAffected", Content = "Nur von Streichung betroffene Wahlen", IsChecked = true, Margin = new Thickness(0, 0, 8, 4) };
        impactFilterPanel.Children.Add(impactOutcomeFilter); impactFilterPanel.Children.Add(impactDepartureFilter); impactFilterPanel.Children.Add(impactAffectedOnly);
        var impact = new StackPanel { Spacing = 8, Name = "PlanningImpact" };
        var reasonBox = new TextBox { Watermark = "Begründung der Entscheidung", Name = "PlanningReason" };
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
                if (before.Status != offer.Status) impact.Children.Add(Text($"{offer.Id}: {before.Status} → {offer.Status}"));
            }
            var affectedCount = preview.Changes.Count(c => !c.RemovedOffers.IsEmpty);
            var reselectionCount = preview.Changes.Count(c => !c.RemovedOffers.IsEmpty && c.Validation.Status == ReviewStatus.Invalid);
            var stillValidCount = preview.Changes.Count(c => !c.RemovedOffers.IsEmpty && c.Validation.Status != ReviewStatus.Invalid);
            impact.Children.Add(Text($"{affectedCount} betroffene Personen ({reselectionCount} nachwahl_nötig, {stillValidCount} nach_streichung_weiterhin_gültig). Vorschau basiert auf {preview.Basis.CreatedAt.LocalDateTime:g}."));
            impact.Children.Add(impactFilterPanel);
            var filtered = preview.Changes.Where(c =>
            {
                if (impactAffectedOnly.IsChecked == true && c.RemovedOffers.IsEmpty) return false;
                var student = preview.Basis.Students.Single(s => s.Student.ExternalStudentId == c.StudentId);
                var followUp = preview.Basis.FollowUps.Single(f => f.StudentId == c.StudentId);
                var needsReselection = followUp.Required || c.Validation.Status == ReviewStatus.Invalid;
                var stillValidAfterCancel = !c.RemovedOffers.IsEmpty && c.Validation.Status != ReviewStatus.Invalid;
                var hasUncheckedSpecialCase = c.Validation.Messages.Any(m => m.Rule.Severity == ReviewStatus.NeedsReview && m.Rule.Id != "M2-RELEASE");
                var outcomeMatch = impactOutcomeFilter.SelectedIndex switch
                {
                    1 => needsReselection,
                    2 => stillValidAfterCancel,
                    3 => hasUncheckedSpecialCase,
                    4 => c.Validation.Status == ReviewStatus.Invalid,
                    _ => true
                };
                var departureMatch = impactDepartureFilter.SelectedIndex switch
                {
                    1 => student.Student.Departure != DepartureStatus.ConfirmedDeparture && (planningIncludeExpected || student.Student.Departure == DepartureStatus.Active),
                    2 => student.Student.Departure != DepartureStatus.Active,
                    _ => true
                };
                return outcomeMatch && departureMatch;
            }).ToArray();
            foreach (var change in filtered)
            {
                var student = preview.Basis.Students.Single(s => s.Student.ExternalStudentId == change.StudentId);
                var stateLabel = !change.RemovedOffers.IsEmpty && change.Validation.Status != ReviewStatus.Invalid
                    ? $"{change.Validation.Status} (weiterhin strukturell gültig)"
                    : change.Validation.Status == ReviewStatus.Invalid ? "Invalid (Nachwahl nötig)" : change.Validation.Status.ToString();
                var noteLabel = change.RemovedOffers.IsEmpty ? "Unverändert" : "Entfällt: " + string.Join(", ", change.RemovedOffers);
                impact.Children.Add(TableLine(student.Student.SyntheticDisplayName, student.Student.ClassName, stateLabel, noteLabel));
                impact.Children.Add(Help("Prüfhinweise", string.Join("\n", change.Validation.Messages.Select(m => m.Rule.Id + ": " + m.Explanation))));
                if (!change.ReplacementOffers.IsEmpty)
                    impact.Children.Add(Help("Mögliche Angebote für eine Ersatzwahl (weiterhin vorläufig prüfen)", string.Join(", ", change.ReplacementOffers)));
            }
            impact.Children.Add(Text("Unbetroffene Wahlen erhalten nur die neue Angebotsversion; ursprüngliche Schülerwünsche bleiben in der Historie. Abgängerfilter wirkt ausschließlich auf Analysen, nicht auf die tatsächliche Streichung."));
            if (preview.Restoring) impact.Children.Add(Text("Die Wiederherstellung betrifft das Angebot. Entfernte Schülerwünsche werden nicht automatisch eingetragen; eine Wahl kann anschließend im Schülerbestand aus ihrer Historie wiederhergestellt werden."));
        }
        impactOutcomeFilter.SelectionChanged += (_, _) => RenderImpact();
        impactDepartureFilter.SelectionChanged += (_, _) => RenderImpact();
        impactAffectedOnly.IsCheckedChanged += (_, _) => RenderImpact();
        changesPanel.Children.Add(Button("Streichung nur simulieren", () =>
        {
            preview = CoursePlanning.PreviewCancellation(Current(), selection.Where(s => s.Box.IsChecked == true).Select(s => s.Offer.Id));
            RenderImpact();
        }));
        changesPanel.Children.Add(Button("Angebot des gespeicherten Stands als Wiederherstellung simulieren", () =>
        {
            var previous = (saved.SelectedItem as PlanningItem)?.Document ?? throw new ArgumentException("Planungsstand auswählen.");
            preview = CoursePlanning.PreviewOfferRestoration(Current(), previous); RenderImpact();
        }));
        changesPanel.Children.Add(Button("Vorschau als separates Szenario speichern", () =>
        {
            if (preview is null) throw new InvalidOperationException("Zuerst simulieren.");
            if (!planning.PlanningDocuments().Any(d => d.Id == preview.Basis.Id)) planning.SavePlanningDocument(preview.Basis);
            planning.SavePlanningDocument(CoursePlanning.Scenario(preview, name.Text ?? ""));
            status.Text = "Szenario gespeichert; aktive Wahlen unverändert."; ShowPlanning();
        }));
        changesPanel.Children.Add(impact); changesPanel.Children.Add(reasonBox); changesPanel.Children.Add(confirmation);
        changesPanel.Children.Add(Primary(Button("Geprüfte Angebotsänderung im Lehrerbestand übernehmen", () =>
        {
            if (preview is null) throw new InvalidOperationException("Zuerst eine Vorschau erstellen.");
            CoursePlanning.Confirm(planning, preview, Actor, reasonBox.Text ?? "", confirmation.IsChecked == true);
            session = new TeacherSession(store!, preview.Catalog, Actor);
            var committedCatalog = preview.Catalog; preview = null; confirmation.IsChecked = false;
            workspace!.Update(workspace.Active! with { Catalog = committedCatalog });
            Refresh(preserveEditor: true); ShowPlanning();
            status.Text = "Angebotsänderung mit Revisionen gespeichert. Aktuelles Fachangebot über Schülerdateien verteilen erneut ausgeben. Offene Nachwahlen im Schülerbestand bearbeiten.";
        })));
        analysisPanel.Children.Add(report);
        RenderPlanningReport(report, Current(), planningGroupSize);
        var wide = Width >= 1150;
        void UpdatePlanningWidth(object? sender, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Property != WidthProperty) return;
            if (wide == (Width >= 1150)) return;
            wide = Width >= 1150;
            RenderPlanningReport(report, (displayed ?? Current()) with { IncludeExpectedDepartures = include.SelectedIndex == 0 }, planningGroupSize);
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
                        .Select(d => $"HJ {d.Semester}: {d.Relevant} verbleibend / {d.All} insgesamt · Gruppen ≈ {d.EstimatedGroups}")));
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
