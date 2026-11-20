using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GsmKurswahl.Application;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Domain;
using GsmKurswahl.Storage;

namespace GsmKurswahl.TeacherApp;

public sealed partial class TeacherWindow
{
    private readonly DispatcherTimer collectionTimer = new() { Interval = TimeSpan.FromSeconds(60) };
    private bool collecting, busy;
    private readonly ProgressBar busyProgress = new() { Name = "BusyProgress", Minimum = 0, Height = 14, MinWidth = 300 };
    private readonly TextBlock busyText = new() { Name = "BusyText", FontSize = 13, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel busyPanel = new() { Spacing = 4, IsVisible = false };
    private readonly TextBox distributionRoot = new() { Watermark = "Laufwerk oder Grundverzeichnis der Klassenordner", Name = "DistributionRoot" };
    private readonly StackPanel deliveryPreview = new() { Spacing = 4 };
    private readonly List<(CheckBox Box, CollectionPreview Preview)> conflictSelection = [];
    private readonly HashSet<string> dismissedConflicts = [];
    private sealed record FolderOption(string? Path)
    {
        public override string ToString() => Path is null ? "Ordner auswählen …" : System.IO.Path.GetFileName(Path);
    }
    private static Grid TableLine(string name, string classroom, string state, string notes, Control? action = null, Control? notesControl = null, Control? stateControl = null)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,80,*,2*,100"), Margin = new Thickness(8, 8) };
        Control[] cells = [Text(name), Text(classroom), stateControl ?? Text(state), notesControl ?? Text(notes), action ?? Text("")];
        for (var i = 0; i < cells.Length; i++) { Grid.SetColumn(cells[i], i); grid.Children.Add(cells[i]); }
        return grid;
    }
    private void ShowImportPreview()
    {
        importResults.Children.Clear();
        if (importFormatNotice is { } notice)
            importResults.Children.Add(new Border
            {
                Name = "ImportFormatNotice", Background = SuccessSoft, BorderBrush = SuccessBorder, BorderThickness = new Thickness(4, 1, 1, 1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 8),
                Child = new TextBlock { Text = notice, TextWrapping = TextWrapping.Wrap }
            });
        var selected = new List<(CheckBox Box, StudentRecord Candidate, int? Version)>();
        var existingStudents = store!.Students();
        var rowsToRender = new List<Border>();
        int countNew = 0, countDiff = 0, countKeep = 0, countError = 0, countSame = 0;

        foreach (var row in importRows)
        {
            var candidate = row.Candidate;
            var current = candidate is null ? null : existingStudents.SingleOrDefault(s => s.Student.ExternalStudentId == candidate.Student.ExternalStudentId);
            var error = row.Error;
            if (candidate is not null && candidate.Student.GraduationYear != session!.Catalog.GraduationYear) error = "Abiturjahrgang passt nicht.";
            if (candidate is not null && error.Length == 0)
            {
                var others = existingStudents.Where(s => s.Student.ExternalStudentId != candidate.Student.ExternalStudentId);
                var candidateHome = candidate.HomeDirectory.Replace('\\', '/').TrimEnd('/');
                if (others.Any(s => (!string.IsNullOrWhiteSpace(candidate.WindowsUsername) && string.Equals(s.WindowsUsername, candidate.WindowsUsername, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(candidate.AccountId) && string.Equals(s.AccountId, candidate.AccountId, StringComparison.OrdinalIgnoreCase))
                    || (candidateHome.Length > 0 && string.Equals(s.HomeDirectory.Replace('\\', '/').TrimEnd('/'), candidateHome, StringComparison.OrdinalIgnoreCase))))
                    error = "Konto, SID oder Ordner bereits einer anderen Person im Bestand zugeordnet.";
            }
            var difference = current is null || candidate is null ? "" : StudentImportService.Differences(current, candidate);
            var identicalSource = current?.Origin is { } origin && candidate?.Origin is { } incoming && origin.ContentHash == incoming.ContentHash && origin.MappingVersion == incoming.MappingVersion;
            var manualCorrection = current is not null && !identicalSource && difference.Length > 0 && StudentImportService.HasManualCorrections(store!, current);
            var box = new CheckBox { Name = candidate is null ? null : "ImportSelect_" + candidate.Student.ExternalStudentId, IsChecked = error.Length == 0 && !manualCorrection, IsEnabled = error.Length == 0 };
            var statusText = error.Length > 0 ? "Fehler" : identicalSource || current is not null && difference.Length == 0 ? "Unverändert" : current is null ? "Neu" : manualCorrection ? "Korrektur behalten" : "Abweichung";
            if (error.Length > 0) countError++;
            else if (statusText == "Unverändert") countSame++;
            else if (statusText == "Neu") countNew++;
            else if (statusText == "Korrektur behalten") countKeep++;
            else countDiff++;
            var detailText = error.Length > 0 ? error : identicalSource ? "Bereits importiert; Korrekturen bleiben" : manualCorrection ? $"Lehreränderung vorhanden ({difference}) · nur mit Häkchen überschreiben" : difference;
            var line = TableLine(candidate?.Student.SyntheticDisplayName ?? $"Zeile {row.Row}", candidate?.Student.ClassName ?? "", statusText, detailText, box);
            ToolTip.SetTip(line, candidate is null ? error : (current is null ? "" : "Bisher:\n" + Describe(current) + "\n\n") + "Import:\n" + Describe(candidate));
            rowsToRender.Add(new Border { Background = error.Length > 0 ? Brushes.MistyRose : difference.Length > 0 && !identicalSource ? Brushes.LemonChiffon : Brushes.Transparent, Child = line });
            if (candidate is not null && error.Length == 0) selected.Add((box, candidate, current?.Version));
        }

        var accept = Button("Alle ausgewählten Schüler übernehmen", () =>
        {
            var pending = selected.Where(r => r.Box.IsChecked == true).ToArray();
            // Check the entire displayed batch before saving its first row.
            if (pending.Any(r => store!.Students().SingleOrDefault(s => s.Student.ExternalStudentId == r.Candidate.Student.ExternalStudentId)?.Version != r.Version))
                throw new InvalidOperationException("Bestand geändert. Bitte Vorschau erneuern.");
            var changed = 0; var unchanged = 0; var failures = new List<string>();
            foreach (var row in pending)
            {
                try
                {
                    if (StudentImportService.Confirm(store!, row.Candidate, row.Version, true)) changed++; else unchanged++;
                    row.Box.IsChecked = false; row.Box.IsEnabled = false;
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException)
                { failures.Add(row.Candidate.Student.SyntheticDisplayName + ": " + ex.Message); }
            }
            Refresh(); importResults.Children.Clear();
            var doneStack = new StackPanel { Spacing = 8 };
            doneStack.Children.Add(new TextBlock
            {
                Text = $"✓ {changed} Schüler übernommen/aktualisiert · {unchanged} unverändert · {failures.Count} nicht übernommen. Wahlen und Historien bleiben erhalten.",
                FontWeight = FontWeight.SemiBold, Foreground = SuccessInk, TextWrapping = TextWrapping.Wrap
            });
            if (CurrentStudents().Any())
            {
                var nextBtn = Primary(Button("Weiter: Ins pädagogische Netz wechseln & verteilen →", () => JumpToWorkflowStep(2)));
                nextBtn.Name = "ImportGoToDistribution";
                doneStack.Children.Add(nextBtn);
            }
            importResults.Children.Add(new Border { Background = SuccessSoft, BorderBrush = SuccessBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Child = doneStack });
            if (failures.Count > 0)
            {
                var failStack = new StackPanel { Spacing = 3 };
                foreach (var failure in failures) failStack.Children.Add(new TextBlock { Text = "Nicht übernommen: " + failure, Foreground = Brush.Parse("#8C1D1D"), TextWrapping = TextWrapping.Wrap });
                importResults.Children.Add(new Border { Background = Brush.Parse("#FCEBEB"), BorderBrush = Brush.Parse("#A32D2D"), BorderThickness = new Thickness(4, 1, 1, 1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10), Child = failStack });
            }
            SetStatus($"{changed} Schüler übernommen/aktualisiert; {unchanged} unverändert; {failures.Count} nicht übernommen. Wahlen und Historien bleiben erhalten.",
                failures.Count > 0 ? FeedbackKind.Warning : FeedbackKind.Success);
        });
        accept.Name = "AcceptStudentBatch";

        var summaryBar = new StackPanel { Spacing = 8 };
        summaryBar.Children.Add(new TextBlock
        {
            Text = $"{importRows.Length} Zeilen geprüft: {countNew} neu · {countDiff} Abweichungen · {countSame} unverändert · {countKeep} Korrektur behalten · {countError} Fehler",
            FontWeight = FontWeight.SemiBold, Foreground = countError > 0 ? Brush.Parse("#785311") : Ink, TextWrapping = TextWrapping.Wrap
        });
        var barActions = new WrapPanel { Orientation = Orientation.Horizontal };
        var acceptPrimary = Primary(accept); acceptPrimary.Margin = new Thickness(0, 0, 10, 6);
        barActions.Children.Add(acceptPrimary);
        var selectAll = Compact(Button("Alle gültigen auswählen", () => { foreach (var s in selected) s.Box.IsChecked = true; }));
        selectAll.Margin = new Thickness(0, 0, 8, 6);
        barActions.Children.Add(selectAll);
        var clearAll = Compact(Button("Auswahl aufheben", () => { foreach (var s in selected) s.Box.IsChecked = false; }));
        clearAll.Margin = new Thickness(0, 0, 0, 6);
        barActions.Children.Add(clearAll);
        summaryBar.Children.Add(barActions);
        importResults.Children.Add(SubtleCard(summaryBar, 12));
        importResults.Children.Add(new Border { Background = SoftAccent, Child = TableLine("Name / ID", "Klasse", "Status", "Abweichungen · Details als Tooltip", Text("Übernehmen")) });
        foreach (var r in rowsToRender) importResults.Children.Add(r);
    }
    private void BuildSimpleDistribution()
    {
        Intro(profilePanel, "3 · Schülerdateien verteilen (im pädagogischen Netz)", "Falls Sie noch im Verwaltungsnetz sind, erstellen Sie zuerst das Paket inkl. Programm (.zip). Im pädagogischen Netz prüfen Sie das vorgegebene Grundverzeichnis der Klassenordner oder wählen ein anderes.");
        profilePanel.Children.Add(NetworkTransferHint(false));
        var box = new StackPanel { Spacing = 10 };
        var rootRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        distributionRoot.Margin = new Thickness(0, 0, 8, 0);
        distributionRoot.PropertyChanged += (_, change) =>
        {
            if (change.Property == TextBox.TextProperty) deliveryPreview.Children.Clear();
        };
        rootRow.Children.Add(distributionRoot);
        var browse = Button("Laufwerk / Grundverzeichnis auswählen …", async () =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Grundverzeichnis mit Klassenordnern", AllowMultiple = false });
            if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path) distributionRoot.Text = path;
        });
        Grid.SetColumn(browse, 1); rootRow.Children.Add(browse);
        box.Children.Add(rootRow);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        var check = Primary(Button("Ziele prüfen und gemeinsame Verteilung vorbereiten", PrepareDelivery)); check.Margin = new Thickness(0, 0, 10, 6);
        var defer = Button("Später verteilen · Übersicht öffnen", () => SetStep(YearSetupStep.Complete)); defer.Margin = new Thickness(0, 0, 0, 6);
        actions.Children.Add(check); actions.Children.Add(defer);
        box.Children.Add(actions);
        profilePanel.Children.Add(Surface(box, 14));
        profilePanel.Children.Add(deliveryPreview);
    }
    private void PrepareDelivery()
    {
        Ready(); deliveryPreview.Children.Clear();
        var root = distributionRoot.Text?.Trim() ?? "";
        var records = CurrentStudents();
        var prepared = new List<(StudentRecord Record, CheckBox? Review)>();
        var manual = new List<(StudentRecord Record, ComboBox Picker, FolderOption[] Options, TextBlock State)>();
        var noFolder = new FolderOption(Path: null);
        var rows = new List<(int Order, Border Row)>();
        foreach (var target in StudentDirectories.PreviewDistribution(root, records))
        {
            var record = target.Student;
            if (target.Path is null)
            {
                Control? notes = null;
                var state = Text("Offen");
                if (target.AvailableFolders.Length > 0)
                {
                    var picker = new ComboBox { Name = "ChooseFolder_" + record.Student.ExternalStudentId, HorizontalAlignment = HorizontalAlignment.Stretch };
                    Avalonia.Automation.AutomationProperties.SetName(picker, "Freien Ordner für " + record.Student.SyntheticDisplayName + " auswählen");
                    manual.Add((record, picker, target.AvailableFolders.Select(p => new FolderOption(p)).ToArray(), state));
                    notes = new StackPanel { Spacing = 4, Children = { Text(target.Detail), picker } };
                }
                rows.Add((0, new Border
                {
                    Background = Brush.Parse("#FCEBEB"),
                    Child = TableLine(record.Student.SyntheticDisplayName, record.Student.ClassName, "Offen", target.Detail, notesControl: notes, stateControl: state)
                }));
                continue;
            }
            CheckBox? review = null;
            if (target.RequiresReview)
            {
                review = new CheckBox { Name = "ConfirmFolder_" + record.Student.ExternalStudentId };
                Avalonia.Automation.AutomationProperties.SetName(review, "Ordner für " + record.Student.SyntheticDisplayName + " bestätigt");
                ToolTip.SetTip(review, "Nur ankreuzen, wenn der vorgeschlagene Ordner zu dieser Person gehört.");
            }
            rows.Add((target.RequiresReview ? 1 : 2, new Border
            {
                Background = target.RequiresReview ? Brush.Parse("#FAEEDA") : Brushes.Transparent,
                Child = TableLine(record.Student.SyntheticDisplayName, record.Student.ClassName, target.RequiresReview ? "Vorschlag" : "Bereit", target.Path, review)
            }));
            prepared.Add((record with { HomeDirectory = target.Path }, review));
        }
        var headerCard = new StackPanel { Spacing = 8 };
        var summary = new TextBlock { FontWeight = FontWeight.SemiBold };
        headerCard.Children.Add(summary);
        headerCard.Children.Add(Text("Prüfen Sie die Zielpfade vor der Sammelbestätigung. Haken Sie passende Namensvorschläge einzeln an. Bei offenen Zeilen können Sie einen freien Ordner derselben Klasse auswählen."));
        var distribute = Primary(Button("", async () =>
        {
            Ready();
            var selected = SelectedTargets();
            if (selected.Length == 0) throw new InvalidOperationException("Keine verteilbaren Schüler. Hinweise in der Tabelle prüfen.");
            if (!string.Equals(root, distributionRoot.Text?.Trim() ?? "", StringComparison.Ordinal)
                || !CurrentStudents().Select(r => (r.Student.ExternalStudentId, r.Version))
                    .SequenceEqual(records.Select(r => (r.Student.ExternalStudentId, r.Version))))
                throw new InvalidOperationException("Grundverzeichnis oder Schülerbestand geändert. Ziele erneut prüfen.");
            var service = new ProfileDistribution(store!); var okReports = new List<string>(); var issueReports = new List<string>(); var count = 0;
            // Everything the background work needs is read from controls here, on the UI thread.
            var actorName = Actor; var catalog = DistributedCatalog(); var programDirectory = EffectiveProgramDirectory();
            await RunWithProgress(selected.Length, async report =>
            {
                StudentProgramSource? program = null;
                if (programDirectory.Length > 0)
                {
                    report(0, "Schülerprogramm wird eingelesen …");
                    try { program = await Task.Run(() => StudentProgramSource.Load(programDirectory)); }
                    catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { issueReports.Add("Schülerprogramm nicht mitverteilt: " + ex.Message); }
                }
                for (var i = 0; i < selected.Length; i++)
                {
                    var record = selected[i];
                    report(i, $"{i + 1} von {selected.Length}: {record.Student.SyntheticDisplayName}");
                    var (line, delivered) = await Task.Run(() => DistributeOne(service, record, root, actorName, catalog, program));
                    if (delivered) { count++; okReports.Add(line); } else issueReports.Add(line);
                }
            });
            deliveryPreview.Children.Clear();
            var resultCard = new StackPanel { Spacing = 6 };
            resultCard.Children.Add(new TextBlock
            {
                Text = $"{count} von {records.Length} Schülerdateien verteilt.",
                FontWeight = FontWeight.SemiBold, Foreground = issueReports.Count == 0 && count == records.Length ? SuccessInk : Brush.Parse("#785311")
            });
            deliveryPreview.Children.Add(new Border
            {
                Background = issueReports.Count == 0 && count == records.Length ? SuccessSoft : Brush.Parse("#FAEEDA"),
                BorderBrush = issueReports.Count == 0 && count == records.Length ? SuccessBorder : Brush.Parse("#C48723"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Child = resultCard
            });
            if (issueReports.Count > 0)
            {
                var failBox = new StackPanel { Spacing = 3 };
                failBox.Children.Add(new TextBlock { Text = $"Hinweise / nicht zugestellt ({issueReports.Count}):", FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#8C1D1D") });
                foreach (var r in issueReports) failBox.Children.Add(new TextBlock { Text = "• " + r, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#8C1D1D") });
                deliveryPreview.Children.Add(new Border { Background = Brush.Parse("#FCEBEB"), BorderBrush = Brush.Parse("#A32D2D"), BorderThickness = new Thickness(4, 1, 1, 1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10), Child = failBox });
            }
            if (okReports.Count > 0)
            {
                var okStack = new StackPanel { Spacing = 2 };
                foreach (var r in okReports) okStack.Children.Add(Text(r));
                deliveryPreview.Children.Add(new Expander { Header = $"Details zu {okReports.Count} erfolgreichen Zustellungen", IsExpanded = false, HorizontalAlignment = HorizontalAlignment.Stretch, Content = okStack });
            }
            if (count == records.Length && workspace!.Active!.Step != YearSetupStep.Complete) SetStep(YearSetupStep.Complete);
            Refresh();
            SetStatus($"{count} von {records.Length} Schülerdateien verteilt." + (issueReports.Count > 0 ? " " + string.Join(" · ", issueReports) : ""),
                issueReports.Count > 0 || count < records.Length ? FeedbackKind.Warning : FeedbackKind.Success);
            await CollectAutomatically();
        }));
        headerCard.Children.Add(distribute);
        deliveryPreview.Children.Add(SubtleCard(headerCard, 12));
        deliveryPreview.Children.Add(new Border { Background = SoftAccent, Child = TableLine("Name", "Klasse", "Status", "Zielordner / Hinweis", Text("Bestätigt")) });
        foreach (var entry in rows.OrderBy(r => r.Order)) deliveryPreview.Children.Add(entry.Row);

        StudentRecord[] SelectedTargets() => prepared.Where(p => p.Review is null || p.Review.IsChecked == true).Select(p => p.Record)
            .Concat(manual.Where(m => m.Picker.SelectedItem is FolderOption { Path: not null })
                .Select(m => m.Record with { HomeDirectory = ((FolderOption)m.Picker.SelectedItem!).Path! }))
            .ToArray();
        var updatingChoices = false;
        void UpdateSummary()
        {
            if (updatingChoices) return;
            updatingChoices = true;
            try
            {
                var selected = SelectedTargets();
                var used = selected.Select(r => r.HomeDirectory).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var (record, review) in prepared)
                    if (review is not null) review.IsEnabled = review.IsChecked == true || !used.Contains(record.HomeDirectory);
                foreach (var (_, picker, options, state) in manual)
                {
                    var current = picker.SelectedItem as FolderOption ?? noFolder;
                    state.Text = current.Path is null ? "Offen" : "Ausgewählt";
                    ToolTip.SetTip(picker, current.Path);
                    var available = options.Where(o => o == current || !used.Contains(o.Path!)).Prepend(noFolder).ToArray();
                    if (picker.ItemsSource is FolderOption[] existing && existing.SequenceEqual(available)) continue;
                    picker.ItemsSource = available;
                    picker.SelectedItem = current;
                }
                var openCount = records.Length - selected.Length;
                summary.Text = $"Verteilungsprüfung: {selected.Length} bereit · {openCount} offen";
                summary.Foreground = openCount > 0 ? Brush.Parse("#785311") : Ink;
                distribute.Content = $"{selected.Length} Zuordnungen bestätigen und Schülerdateien verteilen";
                distribute.IsEnabled = selected.Length > 0;
                SetStatus(summary.Text, openCount > 0 ? FeedbackKind.Warning : FeedbackKind.Info);
            }
            finally { updatingChoices = false; }
        }
        foreach (var (_, review) in prepared)
            if (review is not null) review.IsCheckedChanged += (_, _) => UpdateSummary();
        foreach (var (_, picker, _, _) in manual)
            picker.SelectionChanged += (_, _) => UpdateSummary();
        UpdateSummary();
    }
    private void RenderCollectionStatus(int importedCount, IReadOnlyList<string> messages)
    {
        static string Key(CollectionPreview p) => $"{p.Choice.StudentId}:{p.Origin.ContentHash}:{p.ExpectedActive}:{p.Student.Version}";
        var selections = conflictSelection.ToDictionary(x => Key(x.Preview), x => x.Box.IsChecked == true);
        if (collections.Any(p => !selections.ContainsKey(Key(p)))) conflict.IsChecked = false;
        conflictSelection.Clear(); collectionResults.Children.Clear();
        collectionResults.Children.Add(Text($"Automatisch eingelesen: {importedCount}. Offene Konflikte: {collections.Length}. Zuletzt geprüft: {DateTime.Now:t}"));
        foreach (var preview in collections)
        {
            var box = new CheckBox { IsChecked = selections.GetValueOrDefault(Key(preview), true) };
            conflictSelection.Add((box, preview));
            collectionResults.Children.Add(TableLine(preview.Student.Student.SyntheticDisplayName + " (" + preview.Choice.StudentId + ")", preview.Student.Student.ClassName, "Konflikt", preview.Conflict, box));
        }
        foreach (var message in messages) collectionResults.Children.Add(Text(message));
        if (conflictSection is not null) conflictSection.IsVisible = !collections.IsEmpty;
    }
    // The deadline is a hint in the pupils' copy of the offer; the protected catalog itself stays unchanged.
    private OfferCatalog DistributedCatalog() => session!.Catalog with { SubmissionDeadline = workspace?.Active?.SubmissionDeadline };
    private string EffectiveProgramDirectory()
    {
        var directory = workspace?.Active?.StudentProgramDirectory;
        if (string.IsNullOrWhiteSpace(directory)) directory = SchoolPresets().StudentProgramDirectory;
        if (string.IsNullOrWhiteSpace(directory) && transferPackageDirectory is not null)
        {
            var bundled = Path.Combine(transferPackageDirectory, PedagogicalTransferPackage.StudentFolderName);
            if (Directory.Exists(bundled)) directory = bundled;
        }
        return directory ?? "";
    }
    // Runs off the UI thread: no control access here. The store is not used concurrently, because the window is
    // locked and automatic collection pauses while RunWithProgress is active.
    private (string Line, bool Delivered) DistributeOne(ProfileDistribution service, StudentRecord record, string root, string actorName, OfferCatalog catalog, StudentProgramSource? program)
    {
        try
        {
            StudentDirectories.Resolve(root, record.HomeDirectory);
            var current = store!.Students().Single(s => s.Student.ExternalStudentId == record.Student.ExternalStudentId);
            if (!current.MappingConfirmed || current.HomeDirectory != record.HomeDirectory)
            {
                current = record with { MappingConfirmed = true, Version = record.Version + 1, Actor = actorName, ChangedAt = DateTimeOffset.UtcNow };
                store.SaveStudent(current, record.Version);
            }
            var latest = store.LatestProfile(current.Student.ExternalStudentId);
            if (latest is null || latest.Profile.GraduationYear != current.Student.GraduationYear || System.Text.Json.JsonSerializer.Serialize(latest.Profile.Prerequisites, DocumentJson.Options) != System.Text.Json.JsonSerializer.Serialize(current.Student.Prerequisites, DocumentJson.Options))
                latest = service.Issue(current);
            var result = service.Distribute(current, latest, catalog);
            var programText = "";
            if (program is not null)
            {
                var delivery = service.DeliverProgram(store.Students().Single(s => s.Student.ExternalStudentId == record.Student.ExternalStudentId), program);
                programText = delivery.Status == "verteilt" ? " · Programm bereitgestellt" : " · Programm: " + DistributionText(delivery.Status);
            }
            return (current.Student.SyntheticDisplayName + ": " + result.Status + programText, result.Status == "verteilt");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        { return (record.Student.SyntheticDisplayName + ": " + ex.Message, false); }
    }
    /// <summary>
    /// Long file work with a visible progress bar in the footer. The window stays responsive but locked: navigation,
    /// page and closing are blocked and automatic collection pauses, so nothing else touches the store meanwhile.
    /// </summary>
    private async Task RunWithProgress(int total, Func<Action<int, string>, Task> work)
    {
        var started = DateTime.Now;
        void Report(int done, string text)
        {
            busyProgress.Maximum = Math.Max(total, 1); busyProgress.Value = done;
            var remaining = done > 0 && done < total ? (DateTime.Now - started) / done * (total - done) : (TimeSpan?)null;
            busyText.Text = text + (remaining is { } r ? $" · noch etwa {Math.Ceiling(r.TotalMinutes):0} min" : "");
        }
        if (busy || collecting) throw new InvalidOperationException("Es läuft gerade ein anderer Vorgang (z. B. automatisches Einsammeln). Bitte kurz warten und erneut starten.");
        busy = true; collecting = true;
        busyPanel.IsVisible = true; navigation.IsEnabled = false; pageHost.IsEnabled = false; yearSwitchButton.IsEnabled = false;
        try { Report(0, "Vorbereitung …"); await work(Report); Report(total, "Abgeschlossen"); }
        finally
        {
            busy = false; collecting = false;
            busyPanel.IsVisible = false; navigation.IsEnabled = true; pageHost.IsEnabled = true; yearSwitchButton.IsEnabled = true;
        }
    }
    public static bool FinallyCollected(IAdministrationStore store, string studentId) =>
        store is SqliteAdministrationStore sql && !sql.CollectedFiles(studentId).IsEmpty;
    private async Task CollectAutomatically()
    {
        if (collecting || session is null || store is null) return;
        collecting = true;
        var activeSession = session; var activeStore = store;
        try
        {
            var pending = ImmutableArray.CreateBuilder<CollectionPreview>(); var messages = new List<string>(); var count = 0;
            foreach (var record in activeStore.Students().Where(r => r.MappingConfirmed && r.Student.GraduationYear == activeSession.Catalog.GraduationYear))
            {
                try
                {
                    // Nutzerentscheidung 11.11.2026: after the final collection everything happens in the teacher program.
                    if (FinallyCollected(activeStore, record.Student.ExternalStudentId)) continue;
                    var data = StudentFolderLayout.Data(record.HomeDirectory);
                    var path = Path.Combine(data, "aktuell.json");
                    var loaded = await Task.Run(async () => await new JsonChoiceStore(path, data).LoadAsync());
                    if (session != activeSession || store != activeStore) return;
                    if (loaded is null) continue;
                    var preview = activeSession.Preview(loaded.Choice, record.Student.ExternalStudentId, new(path, loaded.ContentToken, "m3-choice-v1", DateTimeOffset.UtcNow, record.WindowsUsername));
                    if (preview.Duplicate) continue;
                    if (preview.Conflict.Length > 0)
                    {
                        if (!dismissedConflicts.Contains(record.Student.ExternalStudentId + ":" + loaded.ContentToken))
                            pending.Add(preview);
                        continue;
                    }
                    activeSession.Confirm(preview, true, false, "Automatische Sammlung aus bestätigtem Schülerordner"); count++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
                { if (session != activeSession || store != activeStore) return; messages.Add(record.Student.SyntheticDisplayName + ": " + ex.Message); }
            }
            collections = pending.ToImmutable();
            RenderCollectionStatus(count, messages);
            if (count > 0 || collections.Length > 0 || messages.Count > 0)
                SetStatus($"Automatische Sammlung: {count} neue Wahlen; {collections.Length + messages.Count} Hinweise unter Kurswahlen einsammeln.",
                    collections.Length + messages.Count > 0 ? FeedbackKind.Warning : FeedbackKind.Success);
            Refresh(preserveEditor: true); UpdateDashboard();
            if (count > 0 && visiblePage == "results" && pageHost.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "ChoiceSave")?.IsEnabled != true)
                ShowResults();
        }
        catch (Exception ex) { if (session == activeSession) SetError("Automatische Sammlung nicht abgeschlossen: " + ex.Message); }
        finally { collecting = false; }
    }
}
