using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.ImportExport;

namespace GsmKurswahl.TeacherApp;

public sealed partial class TeacherWindow
{
    private Expander? transferBackExpander;

    private Control NetworkTransferHint(bool returning)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = returning
                ? "Nach dem abschließenden Einsammeln und der Klärung offener Konflikte im pädagogischen Netz den aktuellen Jahrgang als Transportdatei sichern. Im Verwaltungsnetz diese Datei öffnen und dort weiterarbeiten.\n\nWichtig nach dem Rückwechsel: Sobald die Transportdatei sicher im Verwaltungsnetz angekommen und dort geöffnet ist, das Lehrerprogramm im pädagogischen Netz schließen und den gesamten entpackten Paketordner (samt Programm, Transportdatei und lokalem Arbeitsstand) sowie die ZIP-Datei vollständig löschen."
                : "Das Verteilen der Schülerdateien und das spätere Einsammeln finden im pädagogischen Netz statt – dort ist das Lehrerprogramm im Allgemeinen nicht vorinstalliert. Erstellen Sie deshalb hier im Verwaltungsnetz ein vollständiges Paket inkl. Programm (.zip) für das pädagogische Netz (oder alternativ nur die Transportdatei). Entpacken Sie das ZIP im pädagogischen Netz vollständig in einen eigenen Ordner im persönlichen Laufwerk (U:) und starten Sie „Lehrerprogramm starten.cmd“.",
            TextWrapping = TextWrapping.Wrap, Foreground = Muted, FontSize = 13
        });
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        if (returning)
        {
            var exportBack = Primary(Button("Stand für Verwaltungsnetz speichern …", () => ExportTransfer(true)));
            exportBack.Margin = new Thickness(0, 0, 8, 6);
            actions.Children.Add(exportBack);
        }
        else
        {
            var exportBundle = Primary(Button("Paket für pädagogisches Netz speichern (.zip inkl. Programm) …", ExportPedagogicalPackage,
                "Erstellt eine ZIP-Datei mit Lehrerprogramm, selbstenthaltendem Schülerprogramm (falls vorhanden), Transportstand (.gsm-transfer), Startdatei und Anleitung für das pädagogische Netz."));
            exportBundle.Name = "ExportTransferBundle";
            exportBundle.Margin = new Thickness(0, 0, 8, 6);
            actions.Children.Add(exportBundle);

            var exportOut = Button("Stand für pädagogisches Netz speichern …", () => ExportTransfer(false),
                "Speichert nur die .gsm-transfer-Datei, falls das Lehrerprogramm im pädagogischen Netz bereits vorhanden ist.");
            exportOut.Margin = new Thickness(0, 0, 8, 6);
            actions.Children.Add(exportOut);
        }
        var openTransfer = Button("Transportdatei öffnen …", ImportTransfer);
        openTransfer.Margin = new Thickness(0, 0, 0, 6);
        actions.Children.Add(openTransfer);
        panel.Children.Add(actions);
        panel.Children.Add(new TextBlock
        {
            Text = returning
                ? "Transportiert wird eine .gsm-transfer-Datei mit Stammdaten, Fachangebot, Wahlen und Historie. Nach dem Öffnen im Verwaltungsnetz bitte nicht mehr im pädagogischen Netz weiterarbeiten, sondern das dortige Programm samt Arbeitsstand löschen."
                : "Im Paket ist der komplette Jahrgangsstand mit Stammdaten, Fachangebot, Wahlen und Historie enthalten. Am Ziel im pädagogischen Netz wird lokal im Paketordner weitergearbeitet; im Verwaltungsnetz währenddessen keine parallelen Änderungen vornehmen.",
            TextWrapping = TextWrapping.Wrap, Foreground = Muted, FontSize = 12
        });
        var expander = new Expander
        {
            Name = returning ? "TransferBackExpander" : "TransferOutExpander",
            Header = returning
                ? "Rückwechsel ins Verwaltungsnetz: Transportdatei sichern & Programm danach löschen"
                : "Wechsel ins pädagogische Netz: Paket mit Programm (.zip) oder Transportdatei erstellen",
            IsExpanded = !returning && transferPackageDirectory is null,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = SubtleCard(panel, 12)
        };
        if (returning) transferBackExpander = expander;
        return expander;
    }

    private Task ExportTransfer() => ExportTransfer(false);

    private async Task ExportTransfer(bool returning)
    {
        Ready();
        if (collecting) throw new InvalidOperationException("Einsammeln läuft noch. Bitte danach erneut sichern.");
        if (collections.Length > 0) throw new InvalidOperationException("Bitte zuerst die offenen Einsammelkonflikte klären; diese Vorschau ist noch nicht im Verwaltungsbestand gespeichert.");
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = returning ? "Jahrgang für Rückwechsel ins Verwaltungsnetz sichern" : "Jahrgang für Netzwechsel sichern",
            DefaultExtension = "gsm-transfer",
            SuggestedFileName = $"Abitur-{session!.Catalog.GraduationYear}-{DateTime.Now:yyyyMMdd-HHmmss}.gsm-transfer"
        });
        var path = file?.TryGetLocalPath(); if (path is null) return;
        WorkspaceTransfer.Export(workspace!.Active! with { Catalog = session!.Catalog }, (SqliteAdministrationStore)store!, path);
        SetStatus(returning
            ? $"Transportdatei für das Verwaltungsnetz gespeichert: {path}. Im Verwaltungsnetz öffnen; sobald sie dort sicher angekommen und geöffnet ist, Programm samt Arbeitsstand/Paketordner im pädagogischen Netz vollständig löschen."
            : $"Transportdatei gespeichert: {path}. Im anderen Netz im Lehrerprogramm über „Transportdatei öffnen …“ laden.",
            FeedbackKind.Success);
    }

    private async Task ExportPedagogicalPackage()
    {
        Ready();
        if (collecting) throw new InvalidOperationException("Einsammeln läuft noch. Bitte danach erneut sichern.");
        if (collections.Length > 0) throw new InvalidOperationException("Bitte zuerst die offenen Einsammelkonflikte klären; diese Vorschau ist noch nicht im Verwaltungsbestand gespeichert.");
        if (store is not SqliteAdministrationStore sql) throw new InvalidOperationException("Paket-Export ist nur aus einem gespeicherten Lehrerbestand möglich.");

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Paket inkl. Programm für pädagogisches Netz speichern",
            DefaultExtension = "zip",
            SuggestedFileName = $"GSM-Kurswahl-Paedagogisches-Netz-Abitur-{session!.Catalog.GraduationYear}-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            FileTypeChoices = [new FilePickerFileType("Paket für pädagogisches Netz (.zip)") { Patterns = ["*.zip"] }]
        });
        var path = file?.TryGetLocalPath(); if (path is null) return;

        var year = workspace!.Active! with { Catalog = session!.Catalog };
        var effectiveStudentProgram = EffectiveProgramDirectory();
        PedagogicalPackageResult? result = null;
        await RunWithProgress(100, async report =>
        {
            result = await Task.Run(() => PedagogicalTransferPackage.Export(
                year,
                sql,
                path,
                AppContext.BaseDirectory,
                effectiveStudentProgram,
                (done, total, text) => Dispatcher.UIThread.Post(() =>
                    report((int)Math.Round(100.0 * done / Math.Max(1, total)), text))));
        });
        if (result is null) return;

        var extras = new List<string>
        {
            result.StudentIncluded ? "inkl. Schülerprogramm" : "ohne Schülerprogramm (kein selbstenthaltender StudentApp-Ordner gefunden)"
        };
        if (!result.TeacherSelfContained)
            extras.Add("Hinweis: Lehrerprogramm benötigt im Zielnetz eine installierte .NET-10-Desktop-Laufzeit");
        SetStatus($"Paket für das pädagogische Netz gespeichert: {result.ArchivePath} ({string.Join("; ", extras)}). Im pädagogischen Netz vollständig in einen eigenen Ordner im persönlichen Laufwerk (U:) entpacken und „{PedagogicalTransferPackage.StarterFileName}“ starten.", FeedbackKind.Success);
    }

    private async Task ImportTransfer()
    {
        ClearError();
        var path = await Pick("Jahrgang nach Netzwechsel öffnen", "Transportdatei", "*.gsm-transfer"); if (path is null) return;
        ShowTransferPreview(path, null);
    }

    private void ShowTransferPreview(string path, string? targetWorkspaceRoot)
    {
        var manifest = WorkspaceTransfer.Inspect(path);
        if (workspace?.Active is null)
        {
            visiblePage = "new";
            heading.Text = "Transportstand übernehmen";
            RebuildNavigation();
            UpdateWorkflowProgress();
            UpdateVisualNavigation();
        }
        var panel = new StackPanel { Spacing = 12 };
        Intro(panel, "Transportstand prüfen", $"Abitur {manifest.Year.Catalog.GraduationYear} · gesichert {manifest.CreatedAt.LocalDateTime:g} · Angebot {manifest.Year.Catalog.Version}");
        panel.Children.Add(Text(targetWorkspaceRoot is not null
            ? $"Dieser Stand stammt aus dem mitgelieferten Paket und wird lokal im Unterordner „{Path.GetFileName(targetWorkspaceRoot)}“ geöffnet. Nach dem Verteilen und abschließenden Einsammeln im pädagogischen Netz bitte die Rück-Transportdatei für das Verwaltungsnetz sichern und danach den gesamten Paketordner wieder löschen."
            : "Dieser Stand wird als neuer lokaler Verwaltungsbestand geöffnet. Der bisherige Bestand bleibt erhalten. Es erfolgt keine Zusammenführung; nach der Übernahme nur mit dem neuen Stand weiterarbeiten."));
        panel.Children.Add(Primary(Button("Transportstand übernehmen und öffnen", () =>
        {
            if (collecting) throw new InvalidOperationException("Einsammeln läuft noch. Bitte kurz warten.");
            var root = targetWorkspaceRoot ?? Path.Combine(TeacherWorkspace.DefaultRoot, "Netzwechsel", $"Abitur-{manifest.Year.Catalog.GraduationYear}-" + Guid.NewGuid().ToString("N"));
            if (targetWorkspaceRoot is not null && Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any())
                Directory.Delete(root);
            var next = WorkspaceTransfer.Import(path, root, manifest);
            if (targetWorkspaceRoot is null && explicitWorkspaceRoot is null)
                new TeacherWorkspaceLocation(TeacherWorkspace.DefaultRoot).Remember(next.Root);
            workspace = next;
            OpenActiveYear();
            if (workspace.Active?.Step == YearSetupStep.Complete && store is not null && session is not null)
            {
                var summary = TeacherDashboardBuilder.Build(store, session);
                if (summary.Students > 0 && summary.Delivered < summary.Students)
                    ShowPage("profiles");
            }
            SetStatus(targetWorkspaceRoot is not null
                ? "Transportstand im Paketordner lokal geöffnet. Jetzt im pädagogischen Netz das Schülerlaufwerk prüfen und Schülerdateien verteilen."
                : "Transportstand lokal geöffnet. Vor Verteilung/Einsammeln das Schülerlaufwerk prüfen. "
                    + (explicitWorkspaceRoot is null ? "Dieser Stand wird beim nächsten Start wieder geöffnet." : "Teststarter öffnet erneut seinen Demo-Bestand; diesen Transportstand bei Bedarf über Einstellungen öffnen."),
                FeedbackKind.Success);
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
        WriteNew(path, PlanningPrint.Demand(document.Catalog.GraduationYear, document.Name + $" | Stand {document.CreatedAt.LocalDateTime:g}" + (document.IsScenario ? " | Szenario" : ""), filter + "; bestätigte Abgänger ausgeschlossen", PlanningPresentation.DemandTable(document), document.Catalog));
        SetStatus("PDF gespeichert: " + path + ". Im PDF-Programm auf Papier drucken (Strg+P).", FeedbackKind.Success);
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { SetStatus((status.Text ?? "") + " Automatisches Öffnen nicht möglich: " + ex.Message, FeedbackKind.Warning); }
    }
    private string CreateReselectionSheets()
    {
        Ready();
        var rows = store!.Students().Select(s => (Student: s, Revision: store.History(s.Student.ExternalStudentId).LastOrDefault(r => (r.Source is RevisionSource.OfferCancellation or RevisionSource.Restoration) && r.OfferVersion == session!.Catalog.Version)))
            .Where(x => x.Revision is { } r && r.Changes.Any(c => c.Before.Length > 0 && c.After.Length == 0)).ToArray();
        if (rows.Length == 0) throw new InvalidOperationException("Keine betroffenen Wahlen zur aktuellen Streichung vorhanden.");
        var folder = Path.Combine(workspace!.Root, "Nachwahlboegen", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        var items = rows.Select(row =>
        {
            var removed = row.Revision!.Changes.Where(c => c.Before.Length > 0 && c.After.Length == 0).Select(c => c.OfferId).ToArray();
            var guidance = DocumentGuidance.BuildReselection(row.Student, row.Revision.Snapshot, session!.Catalog, removed);
            return new ReselectionPrintItem(row.Student, row.Revision.Snapshot, removed)
            {
                Steps = [.. guidance.Steps.Select(x => new ReselectionStep(x.Heading, x.Text, x.Options))],
                SummaryNote = guidance.SummaryNote
            };
        }).ToArray();
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
