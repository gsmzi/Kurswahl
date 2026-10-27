using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
        foreach (var row in importRows)
        {
            var candidate = row.Candidate;
            var current = candidate is null ? null : store!.Students().SingleOrDefault(s => s.Student.ExternalStudentId == candidate.Student.ExternalStudentId);
            var error = row.Error;
            if (candidate is not null && candidate.Student.GraduationYear != session!.Catalog.GraduationYear) error = "Abiturjahrgang passt nicht.";
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
        profilePanel.Children.Add(Button("Ziele prüfen und gemeinsame Verteilung vorbereiten", PrepareDelivery));
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
            var error = paths.Values.Count(p => string.Equals(path, p, StringComparison.OrdinalIgnoreCase)) > 1 ? "Ordner mehreren Personen zugeordnet" :
                string.IsNullOrWhiteSpace(record.AccountId) || record.WindowsUsername.Split('\\').Length != 2 ? "Geprüftes Schulkonto und SID fehlen in der Schülerliste" : "";
            deliveryPreview.Children.Add(TableLine(record.Student.SyntheticDisplayName, record.Student.ClassName, error.Length == 0 ? "Bereit" : "Offen", error.Length == 0 ? path : error));
            if (error.Length == 0) prepared.Add(record with { HomeDirectory = path });
        }
        deliveryPreview.Children.Add(Text("Mit der gemeinsamen Bestätigung bestätigen Sie die angezeigten Konto-/Ordnerzuordnungen. Offene Zeilen bleiben unbearbeitet."));
        deliveryPreview.Children.Add(Button($"{prepared.Count} Zuordnungen bestätigen und Schülerdateien verteilen", async () =>
        {
            Ready();
            if (prepared.Count == 0) throw new InvalidOperationException("Keine verteilbaren Schüler. Hinweise in der Tabelle prüfen.");
            using var certificates = new X509Store(StoreName.My, StoreLocation.CurrentUser); certificates.Open(OpenFlags.ReadOnly);
            var matches = certificates.Certificates.Find(X509FindType.FindByThumbprint, thumbprint.Text ?? "", false);
            if (matches.Count != 1 || string.IsNullOrWhiteSpace(keyId.Text)) throw new InvalidOperationException("Bitte einmalig unter Signierung einrichten das Schulzertifikat und die Schlüsselkennung hinterlegen.");
            using var certificate = matches[0]; using var key = certificate.GetECDsaPrivateKey() ?? throw new InvalidOperationException("Signierschlüssel fehlt.");
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
                    if (latest is null || latest.Profile.WindowsUsername != current.WindowsUsername || latest.Profile.AccountId != current.AccountId || latest.Profile.KeyId != keyId.Text || System.Text.Json.JsonSerializer.Serialize(latest.Profile.Prerequisites, DocumentJson.Options) != System.Text.Json.JsonSerializer.Serialize(current.Student.Prerequisites, DocumentJson.Options)) latest = service.Issue(current, key, keyId.Text!);
                    var result = service.Distribute(current, latest); if (result.Status == "verteilt") count++;
                    reports.Add(current.Student.SyntheticDisplayName + ": " + result.Status);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or CryptographicException) { reports.Add(record.Student.SyntheticDisplayName + ": " + ex.Message); }
            }
            deliveryPreview.Children.Clear(); foreach (var report in reports) deliveryPreview.Children.Add(Text(report));
            if (count == records.Length && workspace!.Active!.Step != YearSetupStep.Complete) SetStep(YearSetupStep.Complete);
            Refresh(); status.Text = $"{count} von {records.Length} Schülerdateien verteilt. " + string.Join(" · ", reports);
            await CollectAutomatically();
        }));
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
                    if (preview.Conflict.Length > 0) { pending.Add(preview); messages.Add(record.Student.SyntheticDisplayName + ": " + preview.Conflict); continue; }
                    activeSession.Confirm(preview, true, false, "Automatische Sammlung aus bestätigtem Schülerordner"); count++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
                { if (session != activeSession || store != activeStore) return; messages.Add(record.Student.SyntheticDisplayName + ": " + ex.Message); }
            }
            collections = pending.ToImmutable(); collectionResults.Children.Clear();
            collectionResults.Children.Add(Text($"Automatisch eingelesen: {count}. Offene Konflikte: {pending.Count}. Zuletzt geprüft: {DateTime.Now:t}"));
            foreach (var message in messages) collectionResults.Children.Add(Text(message));
            if (count > 0 || messages.Count > 0) status.Text = $"Automatische Sammlung: {count} neue Wahlen; {messages.Count} Hinweise unter Kurswahlen einsammeln.";
            Refresh(); UpdateDashboard();
        }
        catch (Exception ex) { if (session == activeSession) status.Text = "Automatische Sammlung nicht abgeschlossen: " + ex.Message; }
        finally { collecting = false; }
    }
}
