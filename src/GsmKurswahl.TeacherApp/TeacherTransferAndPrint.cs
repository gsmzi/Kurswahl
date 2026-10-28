using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.ImportExport;

namespace GsmKurswahl.TeacherApp;

public sealed partial class TeacherWindow
{
    private Control NetworkTransferHint(bool returning)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(Text(returning ? "Zurück ins Verwaltungsnetz" : "Wechsel ins pädagogische Netz"));
        panel.Children.Add(Text(returning
            ? "Nach dem Einsammeln und der Klärung offener Konflikte den aktuellen Jahrgang als Transportdatei sichern. Im Verwaltungsnetz diese Datei öffnen und mit diesem Stand weiterarbeiten."
            : "Vor der Verteilung den aktuellen Jahrgang als Transportdatei sichern. Im pädagogischen Netz im Lehrerprogramm diese Datei öffnen, dort das Schülerlaufwerk zuordnen und verteilen."));
        panel.Children.Add(Button(returning ? "Stand für Verwaltungsnetz speichern …" : "Stand für pädagogisches Netz speichern …", ExportTransfer));
        panel.Children.Add(Button("Transportdatei öffnen …", ImportTransfer));
        panel.Children.Add(Text("Transportiert wird eine .gsm-transfer-Datei mit Stammdaten, Fachangebot, Wahlen und Historie. Nur für die Lehrerverwaltung bestimmt. Am Ziel wird lokal weitergearbeitet; anschließend nicht parallel im alten Stand weiterarbeiten."));
        return Surface(panel, 12);
    }
    private async Task ExportTransfer()
    {
        Ready();
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Jahrgang für Netzwechsel sichern", DefaultExtension = "gsm-transfer",
            SuggestedFileName = $"Abitur-{session!.Catalog.GraduationYear}-{DateTime.Now:yyyyMMdd-HHmmss}.gsm-transfer" });
        var path = file?.TryGetLocalPath(); if (path is null) return;
        if (collecting) throw new InvalidOperationException("Einsammeln läuft noch. Bitte danach erneut sichern.");
        if (collections.Length > 0) throw new InvalidOperationException("Bitte zuerst die offenen Einsammelkonflikte klären; diese Vorschau ist noch nicht im Verwaltungsbestand gespeichert.");
        WorkspaceTransfer.Export(workspace!.Active! with { Catalog = session!.Catalog }, (SqliteAdministrationStore)store!, path);
        status.Text = "Transportdatei gespeichert: " + path + ". Am Ziel im Lehrerprogramm über Transportdatei öffnen laden.";
    }
    private async Task ImportTransfer()
    {
        var path = await Pick("Jahrgang nach Netzwechsel öffnen", "Transportdatei", "*.gsm-transfer"); if (path is null) return;
        var manifest = WorkspaceTransfer.Inspect(path);
        var panel = new StackPanel { Spacing = 12 };
        Intro(panel, "Transportstand prüfen", $"Abitur {manifest.Year.Catalog.GraduationYear} · gesichert {manifest.CreatedAt.LocalDateTime:g} · Angebot {manifest.Year.Catalog.Version}");
        panel.Children.Add(Text("Dieser Stand wird als neuer lokaler Verwaltungsbestand geöffnet. Der bisherige Bestand bleibt erhalten. Es erfolgt keine Zusammenführung; nach der Übernahme nur mit dem neuen Stand weiterarbeiten."));
        panel.Children.Add(Primary(Button("Transportstand übernehmen und öffnen", () =>
        {
            if (collecting) throw new InvalidOperationException("Einsammeln läuft noch. Bitte kurz warten.");
            var root = Path.Combine(TeacherWorkspace.DefaultRoot, "Netzwechsel", $"Abitur-{manifest.Year.Catalog.GraduationYear}-" + Guid.NewGuid().ToString("N"));
            var next = WorkspaceTransfer.Import(path, root, manifest);
            if (explicitWorkspaceRoot is null) new TeacherWorkspaceLocation(TeacherWorkspace.DefaultRoot).Remember(next.Root);
            workspace = next; OpenActiveYear();
            status.Text = "Transportstand lokal geöffnet. Vor Verteilung/Einsammeln das Schülerlaufwerk prüfen. "
                + (explicitWorkspaceRoot is null ? "Dieser Stand wird beim nächsten Start wieder geöffnet." : "Teststarter öffnet erneut seinen Demo-Bestand; diesen Transportstand bei Bedarf über Einstellungen öffnen.");
        })));
        panel.Children.Add(Button("Abbrechen", () => { if (workspace?.Active is null) ShowNewYear(); else ShowPage("start"); }));
        pageHost.Content = Scroll(panel);
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); } File.Move(temp, path, false); }
        finally { File.Delete(temp); }
    }
    private async Task PrintPlanning(PlanningDocument document)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Wahlzahlen als PDF speichern und zum Drucken öffnen", DefaultExtension = "pdf",
            SuggestedFileName = $"Wahlzahlen-{document.Catalog.GraduationYear}-{DateTime.Now:yyyyMMdd-HHmmss}.pdf" });
        var path = file?.TryGetLocalPath(); if (path is null) return;
        var filter = document.IncludeExpectedDepartures ? "Mit voraussichtlichen Abgängern" : "Ohne voraussichtliche Abgänger";
        WriteNew(path, PlanningPrint.Demand(document.Catalog.GraduationYear, document.Name + $" | Stand {document.CreatedAt.LocalDateTime:g}" + (document.IsScenario ? " | Szenario" : ""), filter + "; bestätigte Abgänger ausgeschlossen", PlanningPresentation.DemandTable(document)));
        status.Text = "PDF gespeichert: " + path + ". Im PDF-Programm auf Papier drucken (Strg+P).";
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { status.Text += " Automatisches Öffnen nicht möglich: " + ex.Message; }
    }
    private string CreateReselectionSheets()
    {
        Ready();
        var rows = store!.Students().Select(s => (Student: s, Revision: store.History(s.Student.ExternalStudentId).LastOrDefault(r => (r.Source is RevisionSource.OfferCancellation or RevisionSource.Restoration) && r.OfferVersion == session!.Catalog.Version)))
            .Where(x => x.Revision is { } r && r.Changes.Any(c => c.Before.Length > 0 && c.After.Length == 0)).ToArray();
        if (rows.Length == 0) throw new InvalidOperationException("Keine betroffenen Wahlen zur aktuellen Streichung vorhanden.");
        var folder = Path.Combine(workspace!.Root, "Nachwahlboegen", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        var items = rows.Select(row => new ReselectionPrintItem(row.Student, row.Revision!.Snapshot,
            row.Revision.Changes.Where(c => c.Before.Length > 0 && c.After.Length == 0).Select(c => c.OfferId).ToArray())).ToArray();
        var fileName = "Nachwahlboegen-Abitur-" + session!.Catalog.GraduationYear;
        WriteNew(Path.Combine(folder, fileName + ".pdf"), PlanningPrint.Reselections(items, session.Catalog));
        WriteNew(Path.Combine(folder, fileName + ".docx"), ReselectionWord.Render(items, session.Catalog));
        return folder;
    }
    private void OpenReselectionFolder()
    {
        var folder = Path.Combine(workspace!.Root, "Nachwahlboegen");
        if (!Directory.Exists(folder)) throw new InvalidOperationException("Noch keine Nachwahlbögen erstellt.");
        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }
}
