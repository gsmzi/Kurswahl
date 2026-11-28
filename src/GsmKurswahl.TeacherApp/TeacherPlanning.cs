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
    private Action<string>? planningCancelRequest;
    private Func<bool> planningIsLive = () => false;
    private void ShowPlanning()
    {
        Ready(); ClearError(); visiblePage = "planning";
        var panel = new StackPanel { Spacing = 8 };
        var scroll = Scroll(panel);
        pageHost.Content = scroll; UpdateVisualNavigation();
        var planning = (IPlanningStore)store!;

        var analysisPanel = new StackPanel { Spacing = 10 };
        var historyPanel = new StackPanel { Spacing = 12 };
        var changesPanel = new StackPanel { Spacing = 10 };
        var tabs = new TabControl { Name = "PlanningTabs", ItemsSource = new[] {
            Tab("Auswertung", analysisPanel), Tab("Gespeicherte Stände", historyPanel), Tab("Angebot ändern", changesPanel) }, SelectedIndex = 0 };

        var include = new ComboBox { ItemsSource = new[] { "Mit voraussichtlichen Abgängern", "Ohne voraussichtliche Abgänger" }, SelectedIndex = planningIncludeExpected ? 0 : 1, Name = "PlanningIncludeDepartures", MinWidth = 310 };
        var name = new TextBox { Text = "Planung " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Watermark = "Name des Planungsstands", Name = "PlanningName" };
        var report = new StackPanel { Spacing = 6, Name = "PlanningReport" };
        PlanningDocument Current() => CoursePlanning.Capture(store!, session!.Catalog, Actor, name.Text ?? "", include.SelectedIndex == 0);
        PlanningDocument? displayed = null;
        var sourceLabel = new TextBlock { Text = "Aktueller Bestand", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
        var toolbar = new WrapPanel { Orientation = Orientation.Horizontal };
        include.Margin = new Thickness(0, 0, 12, 4); toolbar.Children.Add(include);
        var currentButton = Compact(Button("Aktuell", () => { displayed = null; sourceLabel.Text = "Aktueller Bestand"; RenderPlanningReport(report, Current(), 24); }));
        currentButton.Margin = new Thickness(0, 0, 10, 4); toolbar.Children.Add(currentButton);
        sourceLabel.Margin = new Thickness(0, 0, 12, 4); toolbar.Children.Add(sourceLabel);
        var printBtn = Compact(Button("Drucken …", () => PrintPlanning(renderedPlanningDocument ?? throw new InvalidOperationException("Zuerst Wahlzahlen anzeigen."))));
        printBtn.Margin = new Thickness(0, 0, 0, 4); toolbar.Children.Add(printBtn);
        ExportTable DemandExport()
        {
            var d = renderedPlanningDocument ?? throw new InvalidOperationException("Zuerst Wahlzahlen anzeigen.");
            return new(["Stand", "Zeitpunkt", "Szenario", "Voraussichtliche Abgänger einbeziehen", "Fach", "LF", "LF bili", "BF", "BF bili", "Wahl"],
                [.. PlanningPresentation.DemandTable(d).Select(r => new[] { d.Name, d.CreatedAt.ToString("O"), d.IsScenario ? "Ja" : "Nein", d.IncludeExpectedDepartures ? "Ja" : "Nein" }.Concat(r).ToArray())]);
        }
        var exportRow = new WrapPanel { Orientation = Orientation.Horizontal };
        var csvBtn = Compact(Button("Angezeigte Wahlzahlen als CSV …", () => ExportTableFile(DemandExport(), false, "Wahlzahlen")));
        csvBtn.Margin = new Thickness(0, 0, 8, 4); exportRow.Children.Add(csvBtn);
        var xlsxBtn = Compact(Button("Angezeigte Wahlzahlen als XLSX …", () => ExportTableFile(DemandExport(), true, "Wahlzahlen")));
        xlsxBtn.Margin = new Thickness(0, 0, 0, 4); exportRow.Children.Add(xlsxBtn);
        analysisPanel.Children.Add(exportRow);
        panel.Children.Add(toolbar); panel.Children.Add(tabs);
        include.SelectionChanged += (_, _) => {
            planningIncludeExpected = include.SelectedIndex == 0;
            var document = (displayed ?? Current()) with { IncludeExpectedDepartures = planningIncludeExpected };
            RenderPlanningReport(report, document, 24);
        };

        var saveCard = new StackPanel { Spacing = 8 };
        saveCard.Children.Add(Heading("Aktuellen Stand sichern"));
        saveCard.Children.Add(Text("Name des zu speichernden Planungsstands"));
        saveCard.Children.Add(name);
        saveCard.Children.Add(Primary(Button("Planungsstand unveränderlich speichern", () => { planning.SavePlanningDocument(Current()); ShowPlanning(); SetStatus("Planungsstand gespeichert. Aktive Wahlen unverändert.", FeedbackKind.Success); })));
        historyPanel.Children.Add(Surface(saveCard, 14));

        var savedItems = planning.PlanningDocuments().Select(d => new PlanningItem(d)).Reverse().ToArray();
        var saved = new ComboBox { ItemsSource = savedItems, PlaceholderText = "Gespeicherten Planungsstand wählen", Name = "PlanningSaved", MinWidth = 340 };
        var compareTarget = new ComboBox { ItemsSource = new object[] { "Aktueller Bestand" }.Concat(savedItems).ToArray(), SelectedIndex = 0, Name = "PlanningCompareTarget", MinWidth = 340 };
        var compareCard = new StackPanel { Spacing = 8 };
        compareCard.Children.Add(Heading("Gespeicherte Stände vergleichen oder wiederherstellen"));
        if (savedItems.Length == 0)
            compareCard.Children.Add(new TextBlock { Text = "Noch keine gespeicherten Planungsstände vorhanden.", Foreground = Muted, FontSize = 13 });
        compareCard.Children.Add(saved);
        compareCard.Children.Add(Text("Vergleichsziel (aktueller Bestand oder zweiter gespeicherter Stand / Szenario)"));
        compareCard.Children.Add(compareTarget);
        var compareActions = new WrapPanel { Orientation = Orientation.Horizontal };
        var compareBtn = Button("Gewählte Planungsstände vergleichen", () =>
        {
            var previous = (saved.SelectedItem as PlanningItem)?.Document ?? throw new ArgumentException("Planungsstand auswählen.");
            var second = (compareTarget.SelectedItem as PlanningItem)?.Document;
            displayed = previous; sourceLabel.Text = previous.Name;
            include.SelectedIndex = previous.IncludeExpectedDepartures ? 0 : 1;
            RenderPlanningReport(report, previous, previous.TargetGroupSize); tabs.SelectedIndex = 0;
            var targetDoc = (second ?? Current()) with { IncludeExpectedDepartures = previous.IncludeExpectedDepartures };
            var targetName = second is null ? "aktuell" : second.Name;
            var before = CoursePlanning.Analyze(previous, previous.TargetGroupSize); var after = CoursePlanning.Analyze(targetDoc, previous.TargetGroupSize);
            report.Children.Add(Heading($"Vergleich mit {targetName} · gleicher Abgängerfilter wie gespeichert"));
            foreach (var id in before.Demand.Select(d => d.OfferId).Union(after.Demand.Select(d => d.OfferId)).Order())
            for (var h = 1; h <= 4; h++)
            {
                var a = before.Demand.SingleOrDefault(d => d.OfferId == id && d.Semester == h)?.Relevant ?? 0;
                var b = after.Demand.SingleOrDefault(d => d.OfferId == id && d.Semester == h)?.Relevant ?? 0;
                if (a != b) report.Children.Add(Text($"{OfferLabel(id)} · HJ {h}: {a} → {b} ({b - a:+0;-0;0})"));
            }
            foreach (var combo in before.Combinations.Select(c => c.Combination).Union(after.Combinations.Select(c => c.Combination)).Order())
            {
                var a = before.Combinations.SingleOrDefault(c => c.Combination == combo)?.Relevant ?? 0;
                var b = after.Combinations.SingleOrDefault(c => c.Combination == combo)?.Relevant ?? 0;
                if (a != b) report.Children.Add(Text($"LF-Kombination {combo}: {a} → {b} ({b - a:+0;-0;0})"));
            }
            report.Children.Add(Text($"Personen: {before.All} → {after.All}; planungsrelevant: {before.Relevant} → {after.Relevant}. Gespeicherter Regelstand: {previous.RuleVersion}."));
        });
        compareBtn.Margin = new Thickness(0, 0, 8, 6);
        compareActions.Children.Add(compareBtn);

        var offers = new WrapPanel { Orientation = Orientation.Horizontal };
        var selection = session!.Catalog.Offers.Where(o => (o.Status is OfferStatus.Offered or OfferStatus.Established) && PlanningPresentation.CanCancel(o))
            .OrderBy(o => o.SubjectId).ThenBy(o => o.Type)
            .Select(o =>
            {
                var subjectName = session.Catalog.Subjects.FirstOrDefault(s => s.Id == o.SubjectId)?.Name ?? o.SubjectId;
                var variantText = o.Variant == CourseVariant.Bilingual ? " bilingual" : "";
                var box = new CheckBox
                {
                    Content = $"{o.SubjectId} · {subjectName} ({FormatCourseType(o.Type)}{variantText})",
                    Name = "PlanningCancel_" + o.Id.Replace('-', '_'),
                    Width = 320,
                    Margin = new Thickness(0, 0, 8, 4)
                };
                ToolTip.SetTip(box, $"Fachangebot {o.Id}: {subjectName} als {FormatCourseType(o.Type)}{(o.Variant == CourseVariant.Bilingual ? " (bilingual)" : "")}");
                return (Offer: o, Box: box);
            }).ToArray();
        foreach (var row in selection) { offers.Children.Add(row.Box); }
        changesPanel.Children.Add(new Expander { Header = "Angebotsvarianten zur Streichung wählen", Content = offers, IsExpanded = true, HorizontalAlignment = HorizontalAlignment.Stretch });
        var impact = new StackPanel { Spacing = 4, Name = "PlanningImpact" };
        var confirmation = new CheckBox { Content = "Diese Vorschau geprüft; Angebotsänderung und neue Verwaltungsrevisionen ausdrücklich übernehmen", Name = "PlanningConfirm" };
        var planningErrorBox = InlineErrorBox("PlanningChangeError", out var planningErrorText);
        confirmation.IsCheckedChanged += (_, _) => SetInlineError(planningErrorBox, planningErrorText, null);
        CancellationPreview? preview = null;
        foreach (var item in selection) item.Box.IsCheckedChanged += (_, _) => { preview = null; confirmation.IsChecked = false; impact.Children.Clear(); SetInlineError(planningErrorBox, planningErrorText, null); };
        void RenderImpact()
        {
            if (preview is null) return;
            confirmation.IsChecked = false; impact.Children.Clear();
            SetInlineError(planningErrorBox, planningErrorText, null);
            foreach (var offer in preview.Catalog.Offers)
            {
                var before = preview.Basis.Catalog.Offers.Single(o => o.Id == offer.Id);
                if (before.Status != offer.Status) impact.Children.Add(Text(offer.Status == OfferStatus.Cancelled ? "Entfällt: " + OfferLabel(offer.Id) : "Angebot geändert: " + OfferLabel(offer.Id)));
            }
            var affected = preview.Changes.Where(c => !c.RemovedOffers.IsEmpty)
                .Select(c => (Change: c, Student: preview.Basis.Students.Single(s => s.Student.ExternalStudentId == c.StudentId)))
                .OrderBy(x => x.Student.Student.ClassName, StringComparer.CurrentCulture)
                .ThenBy(x => x.Student.Student.SortName().Last, StringComparer.CurrentCulture)
                .ThenBy(x => x.Student.Student.SortName().First, StringComparer.CurrentCulture)
                .ThenBy(x => x.Change.StudentId, StringComparer.Ordinal).ToArray();
            impact.Children.Add(new TextBlock { Text = $"{affected.Length} betroffene Schüler · Vorschau, noch keine Änderung gespeichert", FontWeight = FontWeight.SemiBold });
            const string columns = "180,55,170,180,*";
            impact.Children.Add(new Border { Background = SoftAccent, Child = CompactLine(columns, [Text("Schüler"), Text("Klasse"), Text("Entfällt"), Text("Verbleibende LF"), Text("Verbleibende BF / Wahlfächer")]) });
            var rowIndex = 0;
            foreach (var (change, student) in affected)
            {
                var row = CompactLine(columns, [Text(student.Student.DisplayName), Text(student.Student.ClassName),
                    Text(string.Join(", ", change.RemovedOffers.Select(OfferLabel))), Text(PlanningPresentation.Choices(change.Choice, true)), Text(PlanningPresentation.Choices(change.Choice, false))]);
                ToolTip.SetTip(row, string.Join("\n", change.Validation.Messages.Select(m => m.Explanation)));
                impact.Children.Add(new Border { Child = row, Background = rowIndex++ % 2 == 0 ? RowAlt : Brushes.White });
            }
            if (preview.Restoring) impact.Children.Add(Text("Die Wiederherstellung betrifft das Angebot. Entfernte Schülerwünsche werden nicht automatisch eingetragen; eine Wahl kann anschließend im Schülerbestand aus ihrer Historie wiederhergestellt werden."));
        }
        var simulationActions = new WrapPanel { Orientation = Orientation.Horizontal };
        changesPanel.Children.Add(simulationActions);
        var simulateBtn = Button("Streichung nur simulieren", () =>
        {
            preview = CoursePlanning.PreviewCancellation(Current(), selection.Where(s => s.Box.IsChecked == true).Select(s => s.Offer.Id));
            RenderImpact();
        });
        simulateBtn.Margin = new Thickness(0, 0, 8, 6);
        simulationActions.Children.Add(simulateBtn);

        var restoreSimBtn = Button("Angebot des gespeicherten Stands als Wiederherstellung simulieren", () =>
        {
            var previous = (saved.SelectedItem as PlanningItem)?.Document ?? throw new ArgumentException("Planungsstand auswählen.");
            preview = CoursePlanning.PreviewOfferRestoration(Current(), previous); RenderImpact(); tabs.SelectedIndex = 2;
        });
        restoreSimBtn.Margin = new Thickness(0, 0, 0, 6);
        compareActions.Children.Add(restoreSimBtn);
        compareCard.Children.Add(compareActions);
        historyPanel.Children.Add(Surface(compareCard, 14));

        var saveScenarioBtn = Button("Vorschau als separates Szenario speichern", () =>
        {
            try
            {
                if (preview is null) throw new InvalidOperationException("Zuerst simulieren.");
                if (!planning.PlanningDocuments().Any(d => d.Id == preview.Basis.Id)) planning.SavePlanningDocument(preview.Basis);
                planning.SavePlanningDocument(CoursePlanning.Scenario(preview, name.Text ?? ""));
                ShowPlanning(); SetStatus("Szenario gespeichert; aktive Wahlen unverändert.", FeedbackKind.Success);
            }
            catch (Exception ex) { SetInlineError(planningErrorBox, planningErrorText, ex.Message); throw; }
        });
        saveScenarioBtn.Margin = new Thickness(0, 0, 8, 6);
        simulationActions.Children.Add(saveScenarioBtn);

        changesPanel.Children.Add(impact);
        changesPanel.Children.Add(confirmation);
        changesPanel.Children.Add(planningErrorBox);
        changesPanel.Children.Add(Primary(Button("Geprüfte Angebotsänderung im Lehrerbestand übernehmen", () =>
        {
            try
            {
                if (preview is null) throw new InvalidOperationException("Zuerst eine Vorschau erstellen.");
                CoursePlanning.Confirm(planning, preview, Actor, "", confirmation.IsChecked == true);
                session = new TeacherSession(store!, preview.Catalog, Actor);
                var hasRemoved = preview.Changes.Any(c => !c.RemovedOffers.IsEmpty);
                var committedCatalog = preview.Catalog; preview = null; confirmation.IsChecked = false;
                workspace!.Update(workspace.Active! with { Catalog = committedCatalog });
                Refresh(preserveEditor: true); ShowPlanning();
                if (hasRemoved)
                {
                    try
                    {
                        var folder = CreateReselectionSheets();
                        SetStatus("Angebotsänderung gespeichert. Aktuelles Fachangebot erneut verteilen. Nachwahlbögen erstellt: " + folder, FeedbackKind.Success);
                    }
                    catch (Exception ex)
                    {
                        SetStatus("Streichung ist gespeichert. Nachwahlbögen konnten nicht vollständig erstellt werden: " + ex.Message + ". Über Nachwahlbögen erneut erstellen wiederholen.", FeedbackKind.Warning);
                    }
                }
                else
                    SetStatus("Angebotsänderung gespeichert. Aktuelles Fachangebot erneut verteilen.", FeedbackKind.Success);
            }
            catch (Exception ex) { SetInlineError(planningErrorBox, planningErrorText, ex.Message); throw; }
        })));

        var followUpStack = new StackPanel { Spacing = 6 };
        followUpStack.Children.Add(Text("1. Unter „Kurswahlen“ den Filter „Nachwahl offen“ wählen."));
        followUpStack.Children.Add(Text("2. Nachwahlbögen für alle Betroffenen als eine PDF drucken und aushändigen."));
        followUpStack.Children.Add(Text("3. Die Nachwahl dort direkt bei der Person eintragen und speichern."));
        var followUpButtons = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        var openReselBtn = Compact(Button("Nachwahlbögen-Ordner öffnen", OpenReselectionFolder)); openReselBtn.Margin = new Thickness(0, 0, 8, 4);
        var createReselBtn = Compact(Button("Nachwahlbögen jetzt erstellen", () => { var folder = CreateReselectionSheets(); SetStatus("Nachwahlbögen erstellt: " + folder, FeedbackKind.Success); })); createReselBtn.Margin = new Thickness(0, 0, 8, 4);
        var gotoProfBtn = Compact(Button("Zur Profil-Verteilung wechseln", () => ShowPage("profiles"))); gotoProfBtn.Margin = new Thickness(0, 0, 0, 4);
        followUpButtons.Children.Add(openReselBtn); followUpButtons.Children.Add(createReselBtn); followUpButtons.Children.Add(gotoProfBtn);
        followUpStack.Children.Add(followUpButtons);
        changesPanel.Children.Add(new Expander
        {
            Name = "PlanningFollowUp",
            Header = "Nächste Schritte & Nachwahlbögen nach einer Streichung",
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = SubtleCard(followUpStack, 12)
        });

        var cancelHost = new StackPanel { Name = "CancelCard", Spacing = 6 };
        planningIsLive = () => displayed is null;
        planningCancelRequest = offerId =>
        {
            var offer = session!.Catalog.Offers.Single(o => o.Id == offerId);
            var title = ChoiceOverview.OfferTitle(session.Catalog, offer);
            var cancelPreview = CoursePlanning.PreviewCancellation(Current(), [offerId]);
            var affected = cancelPreview.Changes.Where(c => !c.RemovedOffers.IsEmpty)
                .Select(c => (Change: c, Student: cancelPreview.Basis.Students.Single(s => s.Student.ExternalStudentId == c.StudentId)))
                .OrderBy(x => x.Student.Student.ClassName, StringComparer.CurrentCulture).ThenBy(x => x.Student.Student.SortName().Last, StringComparer.CurrentCulture).ToArray();
            cancelHost.Children.Clear();
            var card = new StackPanel { Spacing = 6 };
            card.Children.Add(new TextBlock { Text = $"{title} kommt nicht zustande?", FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Ink });
            card.Children.Add(Text($"Personen mit diesem Kurs: {affected.Length}. Beim Streichen wird er bei allen entfernt und das Angebot gesperrt. Die bisherigen Wahlen bleiben in der Historie; vorher wird automatisch ein Planungsstand gesichert, mit dem sich die Streichung zurücknehmen lässt."));
            var cancelReason = new TextBox { Name = "CancelReason", Watermark = "Begründung (optional), z. B. zu wenige Wahlen" };
            card.Children.Add(cancelReason);
            var cancelButtons = new WrapPanel { Orientation = Orientation.Horizontal };
            var confirm = Primary(Button($"Kurs streichen und bei {(affected.Length == 1 ? "einer Person" : affected.Length + " Personen")} entfernen", () =>
            {
                planning.SavePlanningDocument(cancelPreview.Basis with { Name = "Vor Streichung " + title + " · " + DateTime.Now.ToString("g") });
                CoursePlanning.Confirm(planning, cancelPreview, Actor, string.IsNullOrWhiteSpace(cancelReason.Text) ? "Kurs kommt nicht zustande: " + title : cancelReason.Text!, true);
                session = new TeacherSession(store!, cancelPreview.Catalog, Actor);
                workspace!.Update(workspace.Active! with { Catalog = cancelPreview.Catalog });
                var open = cancelPreview.Changes.Count(c => !c.RemovedOffers.IsEmpty && c.Validation.Status == ReviewStatus.Invalid);
                Refresh(preserveEditor: true); ShowPlanning();
                if (affected.Length > 0)
                {
                    try { CreateReselectionSheets(); }
                    catch (Exception ex) { SetStatus("Kurs gestrichen. Nachwahlbögen konnten nicht erstellt werden: " + ex.Message, FeedbackKind.Warning); return; }
                }
                SetStatus($"{title} gestrichen und bei {(affected.Length == 1 ? "einer Person" : affected.Length + " Personen")} entfernt. {open} brauchen eine Nachwahl: unter „Kurswahlen“ den Filter „Nachwahl offen“ wählen, dort direkt eintragen oder Nachwahlbögen drucken.", FeedbackKind.Success);
            }));
            confirm.Name = "CancelConfirm"; confirm.Margin = new Thickness(0, 0, 8, 0); cancelButtons.Children.Add(confirm);
            var abort = Button("Abbrechen", () => cancelHost.Children.Clear()); abort.Name = "CancelAbort"; cancelButtons.Children.Add(abort);
            card.Children.Add(cancelButtons);
            const string cancelColumns = "190,55,*";
            if (affected.Length > 0) card.Children.Add(CompactLine(cancelColumns, [Text("Schüler"), Text("Klasse"), Text("Folge für die Kurswahl")]));
            foreach (var (change, student) in affected)
            {
                var violations = ChoiceOverview.Violations(change.Validation);
                var consequence = Text(violations.IsEmpty ? "weiterhin ohne Regelverletzung" : "Nachwahl nötig: " + string.Join(" · ", violations));
                consequence.Foreground = violations.IsEmpty ? Green : Red;
                card.Children.Add(CompactLine(cancelColumns, [Text(student.Student.DisplayName), Text(student.Student.ClassName), consequence]));
            }
            cancelHost.Children.Add(new Border { Background = Brush.Parse("#FFF6E5"), BorderBrush = Brush.Parse("#E0B45A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Child = card });
            scroll.Offset = new Vector(0, 0);
        };
        analysisPanel.Children.Add(cancelHost);
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
        var legendContent = new StackPanel { Spacing = 4 };
        legendContent.Children.Add(new TextBlock
        {
            Text = "Eine einzelne Zahl bedeutet durchgehend in allen 4 Halbjahren belegt. " +
                   "Format „KS1/KS2“ zeigt Kursstufe 1 (HJ 1+2) / Kursstufe 2 (HJ 3+4). " +
                   "Format „1:… 2:…“ schlüsselt einzelne Halbjahre auf. Bestätigte Abgänger sind ausgeschlossen.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = Accent
        });
        legendContent.Children.Add(new TextBlock
        {
            Text = "Kommt ein Kurs nicht zustande? Das ✕ neben der Wahlzahl zeigt die Betroffenen und streicht den Kurs nach Bestätigung bei allen.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = Muted
        });
        var legendBox = new Border
        {
            Background = SoftAccent,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8),
            Child = legendContent
        };
        panel.Children.Add(new Expander
        {
            Name = "PlanningLegend",
            Header = "Lesehilfe & Kurse streichen",
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = legendBox
        });
        const string columns = "1.8*,.55*,.7*,1.25*,1*,1.1*";
        var subjects = document.Catalog.Subjects.OrderBy(s => s.Name, StringComparer.CurrentCulture).ToArray();
        var split = Width >= 1150;
        var matrix = new Grid { Name = "PlanningMatrix", ColumnDefinitions = new ColumnDefinitions(split ? "*,16,*" : "*") };
        var blocks = new[] { new StackPanel(), new StackPanel() };
        for (var b = 0; b < (split ? 2 : 1); b++)
        {
            Control HeaderCol(string label, string tip)
            {
                var t = Text(label); t.FontWeight = FontWeight.SemiBold;
                ToolTip.SetTip(t, tip); return t;
            }
            var header = CompactLine(columns, [
                HeaderCol("Fach", "Fachkürzel und Fachbezeichnung"),
                HeaderCol("LF", "Leistungsfach (5-stündig, schriftliche Abiturprüfung)"),
                HeaderCol("LF bili", "Leistungsfach bilingual (in englischer Sprache unterrichtet)"),
                HeaderCol("BF", "Basisfach laut Fachangebot"),
                HeaderCol("BF bili", "Basisfach bilingual (in englischer Sprache unterrichtet)"),
                HeaderCol("Wahl", "Wahlfächer und Seminarkurse")
            ]);
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
                    if (planningCancelRequest is { } request && planningIsLive() && !document.IsScenario
                        && offer.Status is OfferStatus.Offered or OfferStatus.Established && PlanningPresentation.CanCancel(offer))
                    {
                        var offerId = offer.Id;
                        var cancel = new Button { Name = "CancelOffer_" + offer.Id.Replace('-', '_'), Content = "✕", FontSize = 10, Padding = new Thickness(5, 0), MinHeight = 20, MinWidth = 0, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                        ToolTip.SetTip(cancel, "Kurs kommt nicht zustande … (zeigt zuerst die betroffenen Personen)");
                        cancel.Click += (_, _) => { try { request(offerId); } catch (Exception ex) { SetError(ex.Message); } };
                        var line = new StackPanel { Orientation = Orientation.Horizontal }; line.Children.Add(text); line.Children.Add(cancel);
                        content.Children.Add(line);
                    }
                    else content.Children.Add(text);
                }
                return content;
            }
            var label = Text(subject.Id + " · " + subject.Name);
            var row = CompactLine(columns, [label, Cell(CourseType.Lf, CourseVariant.Normal), Cell(CourseType.Lf, CourseVariant.Bilingual),
                Cell(CourseType.Bf, CourseVariant.Normal), Cell(CourseType.Bf, CourseVariant.Bilingual), Cell(CourseType.Elective, CourseVariant.Normal, true)]);
            blocks[split && index >= (subjects.Length + 1) / 2 ? 1 : 0].Children.Add(new Border { Name = "PlanningSubject_" + subject.Id,
                Background = index++ % 2 == 0 ? RowAlt : Brushes.White, Child = row });
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
