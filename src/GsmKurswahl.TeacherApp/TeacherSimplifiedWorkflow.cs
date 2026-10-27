using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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
    private bool collecting;
    private readonly TextBox distributionRoot = new() { Watermark = "Laufwerk oder Grundverzeichnis der Klassenordner", Name = "DistributionRoot" };
    private readonly StackPanel deliveryPreview = new() { Spacing = 4 };
    private readonly List<(CheckBox Box, CollectionPreview Preview)> conflictSelection = [];
    private readonly HashSet<string> dismissedConflicts = [];
    private static Grid TableLine(string name, string classroom, string state, string notes, Control? action = null)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,80,*,2*,100"), Margin = new Thickness(8, 8) };
        Control[] cells = [Text(name), Text(classroom), Text(state), Text(notes), action ?? Text("")];
        for (var i = 0; i < cells.Length; i++) { Grid.SetColumn(cells[i], i); grid.Children.Add(cells[i]); }
        return grid;
    }
    private void ShowImportPreview()
    {
        importResults.Children.Clear();
        importResults.Children.Add(TableLine("Name / ID", "Klasse", "Status", "Abweichungen · Details als Tooltip", Text("Übernehmen")));
        var selected = new List<(CheckBox Box, StudentRecord Candidate, int? Version)>();
        var existingStudents = store!.Students();
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
            var box = new CheckBox { IsChecked = error.Length == 0, IsEnabled = error.Length == 0 };
            var line = TableLine(candidate?.Student.SyntheticDisplayName ?? $"Zeile {row.Row}", candidate?.Student.ClassName ?? "", error.Length > 0 ? "Fehler" : identicalSource || current is not null && difference.Length == 0 ? "Unverändert" : current is null ? "Neu" : "Abweichung", error.Length > 0 ? error : identicalSource ? "Bereits importiert; Korrekturen bleiben" : difference, box);
            ToolTip.SetTip(line, candidate is null ? error : (current is null ? "" : "Bisher:\n" + Describe(current) + "\n\n") + "Import:\n" + Describe(candidate));
            importResults.Children.Add(new Border { Background = error.Length > 0 ? Brushes.MistyRose : difference.Length > 0 && !identicalSource ? Brushes.LemonChiffon : Brushes.Transparent, Child = line });
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
            foreach (var failure in failures) importResults.Children.Add(Text("Nicht übernommen: " + failure));
            status.Text = $"{changed} Schüler übernommen/aktualisiert; {unchanged} unverändert; {failures.Count} nicht übernommen (siehe Tabelle). Wahlen und Historien bleiben erhalten.";
        });
        accept.Name = "AcceptStudentBatch"; importResults.Children.Add(Primary(accept));
    }
    private void BuildSimpleDistribution()
    {
        Intro(profilePanel, "3 · Schülerdateien verteilen", "Grundverzeichnis wählen: darunter Klasse und Nachname.Vorname. Abweichende Ordner können in der Schülerliste stehen. Alle Personen werden gemeinsam geprüft und verteilt.");
        profilePanel.Children.Add(distributionRoot);
        profilePanel.Children.Add(Button("Laufwerk / Grundverzeichnis auswählen …", async () =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Grundverzeichnis mit Klassenordnern", AllowMultiple = false });
            if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path) distributionRoot.Text = path;
        }));
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        var check = Button("Ziele prüfen und gemeinsame Verteilung vorbereiten", PrepareDelivery); check.Margin = new Thickness(0, 0, 10, 0);
        var defer = Button("Später verteilen · Übersicht öffnen", () => SetStep(YearSetupStep.Complete));
        actions.Children.Add(check); actions.Children.Add(defer);
        profilePanel.Children.Add(actions);
        profilePanel.Children.Add(deliveryPreview);
    }
    private void PrepareDelivery()
    {
        Ready(); deliveryPreview.Children.Clear();
        var prepared = new List<StudentRecord>();
        var records = CurrentStudents();
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            try { paths.Add(record.Student.ExternalStudentId, StudentDirectories.Resolve(distributionRoot.Text ?? "", record.HomeDirectory)); }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { deliveryPreview.Children.Add(TableLine(record.Student.SyntheticDisplayName, record.Student.ClassName, "Offen", ex.Message)); }
        }
        foreach (var record in records.Where(r => paths.ContainsKey(r.Student.ExternalStudentId)))
        {
            var path = paths[record.Student.ExternalStudentId];
            var error = paths.Values.Count(p => string.Equals(path, p, StringComparison.OrdinalIgnoreCase)) > 1 ? "Ordner mehreren Personen zugeordnet" : "";
            deliveryPreview.Children.Add(TableLine(record.Student.SyntheticDisplayName, record.Student.ClassName, error.Length == 0 ? "Bereit" : "Offen", error.Length == 0 ? path : error));
            if (error.Length == 0) prepared.Add(record with { HomeDirectory = path });
        }
        deliveryPreview.Children.Add(Text("Mit der gemeinsamen Bestätigung bestätigen Sie die angezeigten Ordnerzuordnungen. Offene Zeilen bleiben unbearbeitet."));
        deliveryPreview.Children.Add(Button($"{prepared.Count} Zuordnungen bestätigen und Schülerdateien verteilen", async () =>
        {
            Ready();
            if (prepared.Count == 0) throw new InvalidOperationException("Keine verteilbaren Schüler. Hinweise in der Tabelle prüfen.");
            if (prepared.Any(r => store!.Students().Single(s => s.Student.ExternalStudentId == r.Student.ExternalStudentId).Version != r.Version)) throw new InvalidOperationException("Schülerbestand geändert. Ziele erneut prüfen.");
            var service = new ProfileDistribution(store!); var reports = new List<string>(); var count = 0;
            foreach (var record in prepared)
            {
                try
                {
                    StudentDirectories.Resolve(distributionRoot.Text ?? "", record.HomeDirectory);
                    var current = store!.Students().Single(s => s.Student.ExternalStudentId == record.Student.ExternalStudentId);
                    if (!current.MappingConfirmed || current.HomeDirectory != record.HomeDirectory)
                    {
                        current = record with { MappingConfirmed = true, Version = record.Version + 1, Actor = Actor, ChangedAt = DateTimeOffset.UtcNow };
                        store.SaveStudent(current, record.Version);
                    }
                    var latest = store.LatestProfile(current.Student.ExternalStudentId);
                    if (latest is null || latest.Profile.GraduationYear != current.Student.GraduationYear || System.Text.Json.JsonSerializer.Serialize(latest.Profile.Prerequisites, DocumentJson.Options) != System.Text.Json.JsonSerializer.Serialize(current.Student.Prerequisites, DocumentJson.Options))
                        latest = service.Issue(current);
                    var result = service.Distribute(current, latest, session!.Catalog); if (result.Status == "verteilt") count++;
                    reports.Add(current.Student.SyntheticDisplayName + ": " + result.Status);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException) { reports.Add(record.Student.SyntheticDisplayName + ": " + ex.Message); }
            }
            deliveryPreview.Children.Clear(); foreach (var report in reports) deliveryPreview.Children.Add(Text(report));
            if (count == records.Length && workspace!.Active!.Step != YearSetupStep.Complete) SetStep(YearSetupStep.Complete);
            Refresh(); status.Text = $"{count} von {records.Length} Schülerdateien verteilt. " + string.Join(" · ", reports);
            await CollectAutomatically();
        }));
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
    }
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
                    var path = Path.Combine(record.HomeDirectory, "aktuell.json");
                    var loaded = await Task.Run(async () => await new JsonChoiceStore(path, record.HomeDirectory).LoadAsync());
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
            if (count > 0 || collections.Length > 0 || messages.Count > 0) status.Text = $"Automatische Sammlung: {count} neue Wahlen; {collections.Length + messages.Count} Hinweise unter Kurswahlen einsammeln.";
            Refresh(preserveEditor: true); UpdateDashboard();
        }
        catch (Exception ex) { if (session == activeSession) status.Text = "Automatische Sammlung nicht abgeschlossen: " + ex.Message; }
        finally { collecting = false; }
    }
}
