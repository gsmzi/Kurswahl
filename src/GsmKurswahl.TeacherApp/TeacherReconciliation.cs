using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.ImportExport;

namespace GsmKurswahl.TeacherApp;

public sealed partial class TeacherWindow
{
    private async Task ExportTableFile(ExportTable table, bool xlsx, string name)
    {
        var extension = xlsx ? "xlsx" : "csv";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Tabelle speichern", DefaultExtension = extension,
            SuggestedFileName = name + "." + extension });
        var path = file?.TryGetLocalPath(); if (path is null) return;
        if (!Path.GetExtension(path).Equals("." + extension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Dateiendung muss ." + extension + " sein.");
        WriteNew(path, xlsx ? TableExport.Xlsx(table) : TableExport.Csv(table));
        status.Text = "Export gespeichert: " + path + ". Prüfstatus beachten; keine Freigabe oder ASV-Schreibschnittstelle.";
    }

    private void ShowReconciliation()
    {
        Ready(); visiblePage = "asv";
        var panel = new StackPanel { Spacing = 10 };
        Intro(panel, "Abgleich und Abschluss", "Allgemeiner CSV/XLSX-Abgleich. Der ASV-spezifische Adapter folgt nach anonymisierter Musterdatei. Keine automatische Übertragung an ASV.");
        panel.Children.Add(Text("Dateiformat: eine Zeile pro Schüler-ID, Fachkennung, Kursart (Lf/Bf/Elective/Seminar), Variante (Normal/Bilingual) und Halbjahr. Jahrgang ist erforderlich. Die Datei muss pro enthaltener Person die vollständige Wahl enthalten. Fehlende Personen bleiben unverändert."));
        panel.Children.Add(Button("Externe Kurswahlen auswählen …", LoadExternalChoices));
        var search = new TextBox { Name = "AsvSearch", Watermark = "Schüler-ID oder Klasse filtern" };
        var reviewOnly = new CheckBox { Content = "Nur offene Prüf- und Nachwahlfälle", Name = "AsvReviewOnly" };
        panel.Children.Add(search); panel.Children.Add(reviewOnly);
        TeacherState[] States() => session!.Overview().Where(s => s.Record.Student.GraduationYear == session.Catalog.GraduationYear
            && (s.Record.Student.ExternalStudentId + " " + s.Record.Student.ClassName).Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)
            && (reviewOnly.IsChecked != true || s.Validation?.Status != ReviewStatus.Valid || s.RequiresReselection)).ToArray();
        panel.Children.Add(Button("Gefilterte Belegungen als CSV …", () => ExportTableFile(TableExport.Choices(States()), false, "Belegungen")));
        panel.Children.Add(Button("Gefilterte Belegungen als XLSX …", () => ExportTableFile(TableExport.Choices(States()), true, "Belegungen")));
        var sql = (SqliteAdministrationStore)store!;
        if (!sql.TransferTrackingEnabled)
        {
            panel.Children.Add(Text("Übertragungsvermerke benötigen Datenbankschema 3. Zuerst Sicherung erstellen; ältere Programme können Schema 3 nicht öffnen."));
            panel.Children.Add(Button("Übertragungsvermerke aktivieren · Datenbankschema 3", () => { sql.EnableTransferTracking(); ShowReconciliation(); }));
        }
        var receiptRows = new StackPanel { Spacing = 6, Name = "AsvReceiptRows" };
        ExportTable ReceiptTable()
        {
            var receipts = sql.TransferReceipts();
            return new(["studentId", "revision", "review", "transfer", "actor", "at", "note"], [.. States().Select(s =>
            {
                var receipt = receipts.LastOrDefault(r => r.StudentId == s.Record.Student.ExternalStudentId);
                var current = receipt is not null && receipt.ChoiceRevisionId == s.Active?.Id && receipt.StudentVersion == s.Record.Version
                    && receipt.CatalogVersion == session!.Catalog.Version && receipt.RuleVersion == s.Active?.RuleVersion;
                return new[] { s.Record.Student.ExternalStudentId, s.Active?.Id.ToString() ?? "", TableExport.Status(s.Validation?.Status),
                    current ? receipt!.Entered ? "manuell_uebertragen" : "offen" : receipt is null ? "offen" : "veraltet",
                    receipt?.Actor ?? "", receipt?.At.ToString("O") ?? "", receipt?.Note ?? "" };
            })]);
        }
        void RenderReceipts()
        {
            receiptRows.Children.Clear();
            var tableRows = ReceiptTable().Rows.ToDictionary(r => r[0]);
            foreach (var s in States())
            {
                var row = tableRows[s.Record.Student.ExternalStudentId];
                receiptRows.Children.Add(Text($"{row[0]} · {row[2]} · {row[3]}"));
                if (!sql.TransferTrackingEnabled || s.Active is null) continue;
                var note = new TextBox { Watermark = "Bearbeitungsvermerk / Ausnahmegrund" }; receiptRows.Children.Add(note);
                void Record(bool entered)
                {
                    sql.RecordTransfer(new(1, Guid.NewGuid(), s.Record.Student.ExternalStudentId, s.Active.Id, s.Record.Version,
                        session!.Catalog.Version, s.Active.RuleVersion, DateTimeOffset.UtcNow, Actor, entered, note.Text ?? ""));
                    RenderReceipts();
                }
                receiptRows.Children.Add(Button("Manuelle ASV-Eingabe dokumentieren", () => Record(true)));
                receiptRows.Children.Add(Button("Übertragungsvermerk wieder öffnen", () => Record(false)));
            }
        }
        panel.Children.Add(Text("Ein Übertragungsvermerk dokumentiert ausschließlich die manuelle Eingabe. needs_review/invalid bleiben sichtbar; der Vermerk ersetzt keine fachliche Freigabe. Änderungen an Wahl oder Stammdaten machen ihn veraltet."));
        panel.Children.Add(Button("Gefilterte Übertragungs-/Ausnahmeliste als CSV …", () => ExportTableFile(ReceiptTable(), false, "Uebertragungsstand")));
        panel.Children.Add(Button("Gefilterte Übertragungs-/Ausnahmeliste als XLSX …", () => ExportTableFile(ReceiptTable(), true, "Uebertragungsstand")));
        search.TextChanged += (_, _) => RenderReceipts(); reviewOnly.IsCheckedChanged += (_, _) => RenderReceipts();
        RenderReceipts(); panel.Children.Add(receiptRows);
        panel.Children.Add(Button("Jahrgang sichern …", ExportTransfer));
        panel.Children.Add(Button("Sicherung prüfen und als neuen Bestand öffnen …", ImportTransfer));
        pageHost.Content = Scroll(panel); UpdateVisualNavigation();
    }

    private async Task LoadExternalChoices()
    {
        var path = await Pick("Externe Kurswahlen", "CSV oder XLSX", "*.csv", "*.xlsx"); if (path is null) return;
        var bytes = await File.ReadAllBytesAsync(path);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var imported = Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? StudentTableImport.Xlsx(new MemoryStream(bytes)) : StudentTableImport.Csv(Encoding.UTF8.GetString(bytes));
        var panel = new StackPanel { Spacing = 8 };
        Intro(panel, "Spalten zuordnen", "Zuordnung bezieht sich nur auf die eingelesene Datei. Fachkennungen müssen zum aktuellen Katalog passen. Keine geratenen ASV-Spalten.");
        var picks = new Dictionary<string, ComboBox>();
        foreach (var field in ChoiceTableImport.Fields)
        {
            panel.Children.Add(Text(field));
            var picker = new ComboBox { ItemsSource = new[] { "— auswählen —" }.Concat(imported.Headers).ToArray(), SelectedIndex = imported.Headers.IndexOf(field) + 1 };
            picks.Add(field, picker); panel.Children.Add(picker);
        }
        var complete = new CheckBox { Content = "Die Datei enthält pro enthaltener Schüler-ID die vollständige Belegung." }; panel.Children.Add(complete);
        panel.Children.Add(Button("Abgleich nur als Vorschau anzeigen", () =>
        {
            if (complete.IsChecked != true) throw new InvalidOperationException("Vollständigkeit der enthaltenen Wahlen zuerst bestätigen.");
            var mapping = picks.Where(p => p.Value.SelectedIndex > 0).ToDictionary(p => p.Key, p => (string)p.Value.SelectedItem!);
            var origin = new ImportOrigin(Path.GetFileName(path), hash, "choice-table-v1:" + JsonSerializer.Serialize(mapping.OrderBy(p => p.Key)), DateTimeOffset.UtcNow, "");
            var preview = new ChoiceReconciliation(store!, session!.Catalog, Actor).Preview(ChoiceTableImport.Read(imported, mapping, session.Catalog), origin);
            ShowExternalPreview(preview, path);
        }));
        panel.Children.Add(Button("Abbrechen", ShowReconciliation)); pageHost.Content = Scroll(panel);
    }

    private void ShowExternalPreview(ReconciliationPreview preview, string path)
    {
        var panel = new StackPanel { Spacing = 8 };
        Intro(panel, "Abweichungen prüfen", "Übernahme nur einzeln nach Prüfung. Jede Übernahme erzeugt eine neue Lehrerrevision mit Herkunft; ursprüngliche Schülerwünsche bleiben erhalten.");
        var filter = new ComboBox { ItemsSource = new[] { "Alle" }.Concat(Enum.GetNames<ReconciliationKind>()).ToArray(), SelectedIndex = 0 }; panel.Children.Add(filter);
        ReconciliationItem[] Items() => preview.Items.Where(i => filter.SelectedIndex == 0 || i.Kind.ToString() == (string?)filter.SelectedItem).ToArray();
        ExportTable Differences() => new(["studentId", "difference", "review", "internal", "external", "detail"], [.. Items().Select(i => new[] {
            i.StudentId, i.Kind.ToString(), TableExport.Status(i.Validation?.Status), Describe(i.Internal?.Snapshot.Selections ?? []), Describe(i.External?.Selections ?? []), i.Detail })]);
        panel.Children.Add(Button("Gefilterten Abgleich als CSV …", () => ExportTableFile(Differences(), false, "Abgleich")));
        panel.Children.Add(Button("Gefilterten Abgleich als XLSX …", () => ExportTableFile(Differences(), true, "Abgleich")));
        var rows = new StackPanel { Spacing = 10 }; panel.Children.Add(rows);
        var applied = new HashSet<string>();
        void Render()
        {
            rows.Children.Clear();
            foreach (var item in Items())
            {
                rows.Children.Add(Text(item.StudentId + " · " + item.Kind + (applied.Contains(item.StudentId) ? " · übernommen" : "")));
                rows.Children.Add(Text(item.Detail));
                rows.Children.Add(Text("Intern: " + Describe(item.Internal?.Snapshot.Selections ?? [])));
                rows.Children.Add(Text("Extern: " + Describe(item.External?.Selections ?? [])));
                rows.Children.Add(Text("Prüfung: " + TableExport.Status(item.Validation?.Status) + "\n" + string.Join("\n", item.Validation?.Messages.Select(m => m.Explanation) ?? [])));
                if (item.Kind != ReconciliationKind.Changed || applied.Contains(item.StudentId)) continue;
                var confirmed = new CheckBox { Content = "Vollständigen Ersatz dieser Belegung geprüft und bestätigt" };
                var reason = new TextBox { Watermark = "Änderungsgrund" }; rows.Children.Add(confirmed); rows.Children.Add(reason);
                rows.Children.Add(Button("Diese externe Wahl übernehmen", async () =>
                {
                    if (Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))) != preview.Origin.ContentHash)
                        throw new IOException("Quelldatei geändert; Abgleich erneut laden.");
                    new ChoiceReconciliation(store!, session!.Catalog, Actor).Confirm(preview, item.StudentId, confirmed.IsChecked == true, reason.Text ?? "");
                    applied.Add(item.StudentId); Render();
                }));
            }
        }
        filter.SelectionChanged += (_, _) => Render(); Render();
        panel.Children.Add(Button("Zurück zu Abgleich und Abschluss", ShowReconciliation)); pageHost.Content = Scroll(panel);
    }
    private static string Describe(IEnumerable<CourseSelection> selections) => string.Join("; ", selections.Select(s => $"{s.SubjectId} {s.Type} {s.Variant} HJ {string.Join(',', s.Semesters)}"));
}
