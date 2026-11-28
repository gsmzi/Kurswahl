using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.ImportExport;

namespace GsmKurswahl.TeacherApp;

public sealed partial class TeacherWindow
{
    private (ReconciliationPreview Preview, string FileName)? activeAsvComparison;
    private Border? asvLoadErrorBox;
    private TextBlock? asvLoadErrorText;
    private StackPanel? asvResultsHost;

    internal ExportTable ReligionCaseTable()
    {
        Ready();
        var cases = ReligionCases.All(session!.Overview().Where(s => s.Record.Student.GraduationYear == session.Catalog.GraduationYear));
        return new(["Name", "Klasse", "Schüler-ID", "Klasse 10", "Belegung Kursstufe", "Mündlich geplant", "Feststellungsprüfung", "Hinweis"],
            [.. cases.Select(c => new[] { c.State.Record.Student.DisplayName, c.State.Record.Student.ClassName, c.State.Record.Student.ExternalStudentId,
                c.Grade10, string.Join(c.ChangeDuringCourse ? " → " : "/", c.Chosen),
                string.Join(", ", c.State.Active!.Snapshot.PlannedOralSubjects.Where(o => o is "ETH" or "ER" or "KR")),
                c.AssessmentSubject is null ? "nein" : "ja, " + c.AssessmentSubject,
                c.ChangeDuringCourse && !c.Chosen.Contains("ETH") ? "Wechsel der Religionsgemeinschaft: Zustimmung beider Religionsgemeinschaften klären"
                    : c.ChangeDuringCourse ? "Abmeldung beantragen; weder Religion noch Ethik mündlich (KOE 3.3)"
                    : c.Grade10 is "KR" or "ER" && c.Chosen.Contains("ETH") ? "Abmeldung vom Religionsunterricht beantragen" : "" })]);
    }

    private async Task ExportReligionCases()
    {
        var table = ReligionCaseTable();
        if (table.Rows.Length == 0) { SetStatus("Keine Person mit Religion/Ethik-Wechsel gegenüber Klasse 10 in den eingelesenen Wahlen.", FeedbackKind.Info); return; }
        await ExportTableFile(table, true, "Religion-Ethik-Feststellungspruefungen");
    }

    private async Task ExportTableFile(ExportTable table, bool xlsx, string name)
    {
        var extension = xlsx ? "xlsx" : "csv";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Tabelle speichern", DefaultExtension = extension,
            SuggestedFileName = name + "." + extension });
        var path = file?.TryGetLocalPath(); if (path is null) return;
        if (!Path.GetExtension(path).Equals("." + extension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Dateiendung muss ." + extension + " sein.");
        WriteNew(path, xlsx ? TableExport.Xlsx(table) : TableExport.Csv(table));
        SetStatus("Export gespeichert: " + path + ".", FeedbackKind.Success);
    }

    private void ShowReconciliation()
    {
        Ready(); ClearError(); visiblePage = "asv";
        var panel = new StackPanel { Spacing = 12 };
        Intro(panel, "ASV-Abgleich", "Liest den ASV-BW-Export „zz Kursstufe Fächerwahl“ (.xlsx oder .csv) ein und prüft die Deckungsgleichheit mit dem Lehrerbestand (Kurse, Halbjahre und geplante mündliche Prüfungsfächer). Es werden keine Daten verändert.");

        var loadCard = new StackPanel { Spacing = 8 };
        loadCard.Children.Add(Heading("ASV-Export einlesen"));
        var loadBtn = Primary(Button("ASV-Export „zz Kursstufe Fächerwahl“ auswählen (.xlsx / .csv) …", async () =>
        {
            var path = await Pick("ASV-Export „zz Kursstufe Fächerwahl“ auswählen", "ASV-Export (Excel oder CSV)", "*.xlsx", "*.csv");
            if (path is null) return;
            var bytes = await File.ReadAllBytesAsync(path);
            LoadAsvComparisonBytes(bytes, Path.GetFileName(path));
        }));
        loadBtn.Name = "AsvLoadButton";
        loadCard.Children.Add(loadBtn);

        asvLoadErrorBox = InlineErrorBox("AsvLoadError", out asvLoadErrorText);
        loadCard.Children.Add(asvLoadErrorBox);

        loadCard.Children.Add(Help("Welcher ASV-Export wird unterstützt?",
            "Pfad in ASV-BW: Datei → Verwaltung → Exportformatbibliothek → Datenbereich „Schüler“ → „zz Kursstufe Fächerwahl“ (.xlsx oder als .csv gespeichert).\n\n" +
            "Was wird geprüft?\n" +
            "• Alle gewählten Fächer, Kursarten (Leistungsfach, Basisfach, Wahlfach) und Halbjahre 1–4.\n" +
            "• Geplante mündliche Prüfungsfächer (m1 und m2 in ASV gegen die geplante mündliche Prüfung im Lehrerbestand).\n" +
            "• Zuordnung ausschließlich über die dauerhafte ASV-ID (keine Namensheuristik).\n" +
            "• Besonderheit Biologie bilingual: Da ASV-BW im Kurswahlexport nicht zwischen normal und bilingual unterscheidet, wird intern „Biologie bilingual“ mit „BIO“ in ASV als deckungsgleich gewertet und mit einem Hinweis ausgewiesen.\n\n" +
            "Im Lehrerbestand werden dabei keine Kurswahlen überschrieben; Abweichungen lassen sich über „In Kurswahlen öffnen →“ oder im Vergleichsexport direkt weiterprüfen."));
        panel.Children.Add(Surface(loadCard, 14));

        asvResultsHost = new StackPanel { Spacing = 12 };
        panel.Children.Add(asvResultsHost);
        RenderAsvComparison();

        pageHost.Content = Scroll(panel);
        UpdateVisualNavigation();
    }

    public void LoadAsvComparisonBytes(byte[] bytes, string fileName)
    {
        Ready();
        if (asvLoadErrorBox is not null && asvLoadErrorText is not null)
            SetInlineError(asvLoadErrorBox, asvLoadErrorText, null);
        try
        {
            var imported = AsvCourseChoiceImport.Read(bytes, fileName, session!.Catalog);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var origin = new ImportOrigin(fileName, hash, "asv-zz-kursstufe-faecherwahl-v1", DateTimeOffset.UtcNow, Actor);
            var missing = imported.MissingIdentities.Select(m => (m.RowNumber, m.DisplayName, m.ClassName)).ToArray();
            var preview = new ChoiceReconciliation(store!, session.Catalog, Actor).Preview(imported.Choices, origin, [.. missing]);
            activeAsvComparison = (preview, fileName);
            RenderAsvComparison();

            var items = preview.Items;
            var changed = items.Count(i => i.Kind == ReconciliationKind.Changed);
            var onlyExternal = items.Count(i => i.Kind == ReconciliationKind.OnlyExternal);
            var invalid = items.Count(i => i.Kind == ReconciliationKind.InvalidIdentity);
            var onlyInternalActive = items.Count(i => i.Kind == ReconciliationKind.OnlyInternal && i.Student?.Student.Departure != DepartureStatus.ConfirmedDeparture);
            if (changed == 0 && onlyExternal == 0 && invalid == 0 && onlyInternalActive == 0)
                SetStatus($"ASV-Abgleich abgeschlossen ({fileName}): Alle {items.Count(i => i.Kind == ReconciliationKind.Identical)} Personen sind deckungsgleich.", FeedbackKind.Success);
            else
                SetStatus($"ASV-Abgleich abgeschlossen ({fileName}): Abweichungen: {changed}, {onlyInternalActive} aktive nur in GSM-Kurswahl, {onlyExternal} nur in ASV, {invalid} unklare/fehlende ASV-ID.", FeedbackKind.Warning);
        }
        catch (Exception ex)
        {
            if (asvLoadErrorBox is not null && asvLoadErrorText is not null)
                SetInlineError(asvLoadErrorBox, asvLoadErrorText, ex.Message);
            SetError(ex.Message);
        }
    }

    private void RenderAsvComparison()
    {
        if (asvResultsHost is null || session is null || store is null) return;
        asvResultsHost.Children.Clear();

        if (activeAsvComparison is not { } current)
        {
            asvResultsHost.Children.Add(SubtleCard(new TextBlock
            {
                Text = "Noch keine ASV-Datei geladen. Wählen Sie oben den ASV-BW-Export „zz Kursstufe Fächerwahl“ aus, um die Deckungsgleichheit zu prüfen.",
                Foreground = Muted,
                TextWrapping = TextWrapping.Wrap
            }, 14));
            return;
        }

        var items = current.Preview.Items;
        var identical = items.Count(i => i.Kind == ReconciliationKind.Identical);
        var changed = items.Count(i => i.Kind == ReconciliationKind.Changed);
        var onlyInternal = items.Count(i => i.Kind == ReconciliationKind.OnlyInternal);
        var onlyInternalActive = items.Count(i => i.Kind == ReconciliationKind.OnlyInternal && i.Student?.Student.Departure != DepartureStatus.ConfirmedDeparture);
        var onlyExternal = items.Count(i => i.Kind == ReconciliationKind.OnlyExternal);
        var invalid = items.Count(i => i.Kind == ReconciliationKind.InvalidIdentity);

        var allGreen = changed == 0 && onlyExternal == 0 && invalid == 0 && onlyInternalActive == 0;
        var summaryText = new TextBlock
        {
            Name = "AsvSummary",
            Text = $"Datei: {current.FileName} · {identical} deckungsgleich · Abweichungen: {changed} · {onlyInternal} nur in GSM-Kurswahl · {onlyExternal} nur in ASV · {invalid} unklare/fehlende ASV-ID",
            FontWeight = FontWeight.SemiBold,
            Foreground = allGreen ? Green : (changed > 0 || invalid > 0 ? Brush.Parse("#8C1D1D") : Yellow),
            TextWrapping = TextWrapping.Wrap
        };
        asvResultsHost.Children.Add(new Border
        {
            Background = allGreen ? GreenSoft : (changed > 0 || invalid > 0 ? RedSoft : YellowSoft),
            BorderBrush = allGreen ? SuccessBorder : (changed > 0 || invalid > 0 ? Red : Brush.Parse("#C48723")),
            BorderThickness = new Thickness(4, 1, 1, 1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10),
            Child = summaryText
        });

        var filterCard = new StackPanel { Spacing = 8 };
        var search = new TextBox { Name = "AsvSearch", Watermark = "Name, Klasse, ASV-ID oder Abweichung filtern", Width = 310, Margin = new Thickness(0, 0, 8, 6) };
        var kindFilter = new ComboBox
        {
            Name = "AsvKindFilter",
            ItemsSource = new[]
            {
                "Nur Abweichungen & Auffälligkeiten",
                "Alle",
                "Abweichung",
                "Nur in GSM-Kurswahl",
                "Nur in ASV",
                "Unklare / fehlende ASV-ID",
                "Deckungsgleich"
            },
            SelectedIndex = 0,
            MinWidth = 260,
            Margin = new Thickness(0, 0, 8, 6)
        };
        var resetFilter = Compact(new Button
        {
            Name = "AsvResetFilters",
            Content = "Filter zurücksetzen",
            IsEnabled = false,
            Margin = new Thickness(0, 0, 0, 6),
            VerticalAlignment = VerticalAlignment.Center
        });

        static string DisplayName(ReconciliationItem item) =>
            item.Student?.Student.DisplayName is { Length: > 0 } internalName
                ? internalName
                : item.External?.DisplayName ?? "";

        static string ClassName(ReconciliationItem item) =>
            item.Student?.Student.ClassName is { Length: > 0 } internalClass
                ? internalClass
                : item.External?.ClassName ?? "";

        ReconciliationItem[] FilteredItems()
        {
            var query = search.Text?.Trim() ?? "";
            var selected = kindFilter.SelectedIndex;
            return [.. items.Where(item =>
            {
                var kindMatch = selected switch
                {
                    0 => item.Kind != ReconciliationKind.Identical,
                    2 => item.Kind == ReconciliationKind.Changed,
                    3 => item.Kind == ReconciliationKind.OnlyInternal,
                    4 => item.Kind == ReconciliationKind.OnlyExternal,
                    5 => item.Kind == ReconciliationKind.InvalidIdentity,
                    6 => item.Kind == ReconciliationKind.Identical,
                    _ => true
                };
                if (!kindMatch) return false;
                if (query.Length == 0) return true;
                var text = string.Join(" ", [
                    item.StudentId,
                    DisplayName(item),
                    ClassName(item),
                    item.Detail,
                    .. item.Differences,
                    .. item.Notes
                ]);
                return text.Contains(query, StringComparison.OrdinalIgnoreCase);
            })];
        }

        ExportTable BuildExportTable() => new(
            ["ASV-ID", "Name", "Klasse", "Status", "Abweichungen", "Hinweise", "Intern (Kurse)", "ASV (Kurse)", "Intern (mündlich)", "ASV (mündlich)"],
            [.. FilteredItems().Select(i => new[]
            {
                i.StudentId,
                DisplayName(i),
                ClassName(i),
                FormatAsvStatus(i.Kind),
                i.Differences.Length > 0 ? string.Join(" | ", i.Differences) : (i.Kind == ReconciliationKind.Identical ? "" : i.Detail),
                string.Join(" | ", i.Notes),
                i.Internal is null ? "" : Describe(i.Internal.Snapshot.Selections),
                i.External is null ? "" : Describe(i.External.Selections),
                i.Internal is null ? "" : FormatOral(i.Internal.Snapshot.PlannedOralSubjects),
                i.External is null ? "" : FormatOral(i.External.PlannedOralSubjects)
            })]);

        var filterRow = new WrapPanel { Orientation = Orientation.Horizontal };
        filterRow.Children.Add(search); filterRow.Children.Add(kindFilter); filterRow.Children.Add(resetFilter);
        filterCard.Children.Add(filterRow);

        var exportRow = new WrapPanel { Orientation = Orientation.Horizontal };
        var xlsxBtn = Compact(Button("Abgleich als XLSX …", () => ExportTableFile(BuildExportTable(), true, "ASV-Abgleich")));
        xlsxBtn.Name = "AsvExportXlsx"; xlsxBtn.Margin = new Thickness(0, 0, 8, 4);
        var csvBtn = Compact(Button("Abgleich als CSV …", () => ExportTableFile(BuildExportTable(), false, "ASV-Abgleich")));
        csvBtn.Name = "AsvExportCsv"; csvBtn.Margin = new Thickness(0, 0, 0, 4);
        exportRow.Children.Add(xlsxBtn); exportRow.Children.Add(csvBtn);
        filterCard.Children.Add(exportRow);
        asvResultsHost.Children.Add(Surface(filterCard, 14));

        var rows = new StackPanel { Spacing = 8, Name = "AsvResultRows" };
        asvResultsHost.Children.Add(rows);

        void RenderRows()
        {
            if (session is null || store is null) return;
            rows.Children.Clear();
            resetFilter.IsEnabled = !string.IsNullOrWhiteSpace(search.Text) || kindFilter.SelectedIndex != 0;
            var filtered = FilteredItems();
            if (filtered.Length == 0)
            {
                var emptyText = kindFilter.SelectedIndex == 0 && string.IsNullOrWhiteSpace(search.Text) && items.All(i => i.Kind == ReconciliationKind.Identical)
                    ? "Alle geprüften Einträge sind deckungsgleich."
                    : "Keine Einträge für diesen Filter.";
                rows.Children.Add(SubtleCard(new TextBlock { Text = emptyText, Foreground = Muted }));
                return;
            }

            foreach (var item in filtered)
            {
                var card = new StackPanel { Spacing = 6 };
                var titleName = DisplayName(item);
                var cls = ClassName(item);
                var headerText = (titleName.Length > 0 ? titleName : item.StudentId)
                    + (cls.Length > 0 ? $" · Kl. {cls}" : "")
                    + $" · {item.StudentId}";

                var (badgeBg, badgeFg) = item.Kind switch
                {
                    ReconciliationKind.Identical => (GreenSoft, Green),
                    ReconciliationKind.Changed or ReconciliationKind.InvalidIdentity => (RedSoft, Red),
                    _ => (YellowSoft, Yellow)
                };

                var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                headerGrid.Children.Add(new TextBlock
                {
                    Text = headerText,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                });
                var badge = new Border
                {
                    Background = badgeBg,
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(10, 4),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = FormatAsvStatus(item.Kind), FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = badgeFg }
                };
                Grid.SetColumn(badge, 1);
                headerGrid.Children.Add(badge);
                card.Children.Add(headerGrid);

                if (item.Differences.Length > 0)
                {
                    foreach (var diff in item.Differences)
                        card.Children.Add(new TextBlock { Text = "✗ " + diff, Foreground = Brush.Parse("#8C1D1D"), TextWrapping = TextWrapping.Wrap, FontSize = 13 });
                }
                else if (item.Kind != ReconciliationKind.Identical)
                {
                    card.Children.Add(new TextBlock
                    {
                        Text = "• " + item.Detail,
                        Foreground = item.Kind == ReconciliationKind.InvalidIdentity ? Brush.Parse("#8C1D1D") : Yellow,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 13
                    });
                }

                foreach (var note in item.Notes)
                    card.Children.Add(new TextBlock { Text = "ℹ " + note, Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 });

                if (item.Internal is not null || item.External is not null)
                {
                    var intSummary = item.Internal is null
                        ? "keine Kurswahl gespeichert"
                        : Describe(item.Internal.Snapshot.Selections) + " · mündlich: " + FormatOral(item.Internal.Snapshot.PlannedOralSubjects);
                    var extSummary = item.External is null
                        ? "nicht enthalten"
                        : (item.External.Selections.IsEmpty ? "keine Kurse" : Describe(item.External.Selections))
                            + " · mündlich: " + FormatOral(item.External.PlannedOralSubjects);
                    card.Children.Add(new TextBlock { Text = "Intern: " + intSummary, Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
                    card.Children.Add(new TextBlock { Text = "ASV: " + extSummary, Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
                }

                if (item.Student is not null)
                {
                    var openBtn = Compact(Button("In Kurswahlen öffnen →", () => { choiceSelectedId = item.StudentId; ShowResults(); }));
                    openBtn.Name = "AsvOpenChoice_" + item.StudentId;
                    openBtn.HorizontalAlignment = HorizontalAlignment.Left;
                    card.Children.Add(openBtn);
                }

                rows.Children.Add(Surface(card, 12));
            }
        }

        resetFilter.Click += (_, _) => { search.Text = ""; kindFilter.SelectedIndex = 0; RenderRows(); };
        search.TextChanged += (_, _) => RenderRows();
        kindFilter.SelectionChanged += (_, _) => RenderRows();
        RenderRows();
    }

    private static string FormatAsvStatus(ReconciliationKind kind) => kind switch
    {
        ReconciliationKind.Identical => "Deckungsgleich",
        ReconciliationKind.Changed => "Abweichung",
        ReconciliationKind.OnlyInternal => "Nur in GSM-Kurswahl",
        ReconciliationKind.OnlyExternal => "Nur in ASV",
        ReconciliationKind.InvalidIdentity => "Unklare / fehlende ASV-ID",
        _ => kind.ToString()
    };

    private string FormatOral(IEnumerable<string> subjectIds)
    {
        var names = subjectIds
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(SubjectName)
            .ToArray();
        return names.Length == 0 ? "keine" : string.Join(", ", names);
    }

    private string Describe(IEnumerable<CourseSelection> selections) =>
        string.Join("; ", selections.Select(s => $"{OfferLabel(s.OfferId)} HJ {string.Join(',', s.Semesters)}"));
}
