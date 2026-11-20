using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GsmKurswahl.Application;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Domain;
using GsmKurswahl.ImportExport;
using GsmKurswahl.Storage;

namespace GsmKurswahl.TeacherApp;

public sealed partial class TeacherWindow : Window
{
    private IAdministrationStore? store;
    private TeacherSession? session;
    private readonly TextBlock status = new() { Text = "Lokalen Verwaltungsbestand und Jahrgangskatalog öffnen. Nur synthetische Daten verwenden.", TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel roster = new() { Spacing = 2 };
    private readonly Expander personEditor = new() { Header = "Person in der Liste links öffnen", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
    private readonly StackPanel detail = new() { Spacing = 8 };
    private readonly StackPanel importPanel = new() { Spacing = 8 };
    private readonly StackPanel profilePanel = new() { Spacing = 8 };
    private readonly StackPanel collectionPanel = new() { Spacing = 8 };
    private readonly StackPanel catalogPanel = new() { Spacing = 8 };
    private readonly TextBox filter = new() { Watermark = "Name, Klasse oder Schüler-ID" };
    private readonly CheckBox followupFilter = new() { Content = "Nur offene Nachwahlen" };
    private readonly ComboBox statusFilter = new() { ItemsSource = new[] { "Alle Prüfstände", "Ohne Wahl", "Regelverstoß", "Fachlich zu klären", "Formal gültig", "Religion/Ethik: Wechsel", "Feststellungsprüfung nötig" }, SelectedIndex = 0 };
    private readonly TextBox actor = new() { Text = "demo-beratung", Watermark = "Bearbeiterkennung" };
    private string Actor => actor.Text?.Trim() is { Length: > 0 } value ? value : throw new ArgumentException("Bearbeiterkennung fehlt. Unter Einstellungen eintragen.");
    private readonly ImportColumnPicker mapping = new();
    private ImportTable? table;
    private ImportOrigin? importOrigin;
    private ImmutableArray<StudentImportRow> importRows = [];
    private string? importFormatNotice;
    private readonly StackPanel importResults = new() { Spacing = 5 };
    private ImmutableArray<CollectionPreview> collections = [];
    private readonly StackPanel collectionResults = new() { Spacing = 5 };
    private readonly CheckBox conflict = new() { Content = "Angezeigte Konflikte bewusst zugunsten der eingelesenen Schülerwahl entscheiden" };
    private readonly TextBox reason = new() { Watermark = "Begründung (wird im Änderungsprotokoll gespeichert)" };
    private readonly ListBox profileTargets = new() { SelectionMode = SelectionMode.Multiple, MinHeight = 180 };
    private Expander? importMappingExpander;
    private Border? conflictSection;
    private string? selectedStudentId;
    private sealed record Target(StudentRecord Record) { public override string ToString() => $"{Record.Student.DisplayName} ({Record.Student.ClassName}) → {(Record.HomeDirectory.Length == 0 ? "kein Ordner" : Record.HomeDirectory)} · Ordner {(Record.MappingConfirmed ? "bestätigt" : "noch nicht bestätigt")}"; }
    private sealed record SubjectItem(string? Id, string Label) { public override string ToString() => Label; }

    private static TabItem Tab(string name, Control content) => new() { Header = name, Content = content };
    private static ScrollViewer Scroll(Control content) => new() { Content = content, Margin = new Thickness(8) };
    private Button Button(string label, Func<Task> action, string? help = null)
    {
        var button = new Button { Content = label };
        if (help is not null) ToolTip.SetTip(button, new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
        button.Click += async (_, _) => { button.IsEnabled = false; ClearError(); try { await action(); UpdateDashboard(); UpdateVisualNavigation(); UpdateWorkflowProgress(); } catch (Exception ex) { SetError(ex.Message); } finally { button.IsEnabled = true; } };
        return button;
    }
    private Button Button(string label, Action action, string? help = null) => Button(label, () => { action(); return Task.CompletedTask; }, help);
    private static TextBlock Text(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap };
    private static TextBlock Heading(string value) => new() { Text = value, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private static Expander Help(string title, string explanation) => new()
    { Header = title, Content = new TextBlock { Text = explanation, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12), MaxWidth = 850 }, HorizontalAlignment = HorizontalAlignment.Stretch };
    private static void Intro(StackPanel panel, string heading, string explanation)
    {
        panel.Children.Add(new TextBlock { Text = heading, FontSize = 23, FontWeight = FontWeight.SemiBold, Foreground = Ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) });
        panel.Children.Add(new TextBlock { Text = explanation, Foreground = Muted, TextWrapping = TextWrapping.Wrap, MaxWidth = 1000, HorizontalAlignment = HorizontalAlignment.Left, LineHeight = 22, Margin = new Thickness(0, 0, 0, 8) });
    }
    private static Control Labeled(string label, Control control, string help)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("270,*") };
        var caption = new TextBlock { Text = label + " ⓘ", VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        ToolTip.SetTip(caption, new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
        ToolTip.SetTip(control, new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
        Grid.SetColumn(control, 1); row.Children.Add(caption); row.Children.Add(control); return row;
    }
    // Enum values stay the selected items (stable for storage and tests); only the display is German.
    private static ComboBox EnumBox<T>(string name, T selected, Func<T, string> format, double width = 200) where T : struct, Enum => new()
    {
        Name = name, ItemsSource = Enum.GetValues<T>(), SelectedItem = selected, Width = width,
        ItemTemplate = new FuncDataTemplate<T>((value, _) => new TextBlock { Text = format(value) })
    };
    private string SubjectName(string subjectId) => session?.Catalog.Subjects.FirstOrDefault(s => s.Id == subjectId)?.Name ?? subjectId;
    private string OfferLabel(string offerId)
    {
        var offer = session?.Catalog.Offers.FirstOrDefault(o => o.Id == offerId);
        return offer is null ? offerId : SubjectName(offer.SubjectId) + " " + FormatCourseType(offer.Type) + (offer.Variant == CourseVariant.Bilingual ? " bilingual" : "");
    }
    private static string Semesters(IEnumerable<int> semesters) => semesters.Any() ? "HJ " + string.Join(",", semesters.Order()) : "nicht belegt";

    private static StackPanel BuildOrientation()
    {
        var panel = new StackPanel { Spacing = 14, MaxWidth = 950, HorizontalAlignment = HorizontalAlignment.Left };
        Intro(panel, "Der Jahresablauf auf einen Blick", "Die Kurswahl findet einmal im Jahr statt. Diese Seite fasst zusammen, was wann zu tun ist. Auf der Übersicht zeigt das Programm außerdem jederzeit den nächsten sinnvollen Schritt an.");
        var stepsCard = new StackPanel { Spacing = 8 };
        foreach (var step in new[] {
            "1 · Neuen Abiturjahrgang anlegen – oben neben dem Jahrgang auf „Jahrgang wechseln / neu …“ klicken.",
            "2 · Leistungsfächer auswählen, die in diesem Jahrgang angeboten werden.",
            "3 · Schülerliste (Excel/CSV aus Klasse 10) im Verwaltungsnetz einlesen: Namen, Klassen und Vorbildung.",
            "4 · Wechsel ins pädagogische Netz & Schülerdateien verteilen: Im Verwaltungsnetz ein Paket inkl. Programm (.zip) speichern, im pädagogischen Netz vollständig im persönlichen Laufwerk (U:) entpacken, über „Lehrerprogramm starten.cmd“ öffnen und Profile + Fachangebot auf das Schülerlaufwerk verteilen.",
            "5 · Schüler wählen im pädagogischen Netz im Schülerprogramm. Das dortige Lehrerprogramm sammelt die Wahlen automatisch ein.",
            "6 · Im pädagogischen Netz abschließend einsammeln, Rück-Transportdatei (.gsm-transfer) für das Verwaltungsnetz sichern und dort öffnen. Sobald die Datei sicher im Verwaltungsnetz angekommen und geöffnet ist, Programm und Arbeitsstand im pädagogischen Netz vollständig löschen. Ab dann läuft alles wieder im Verwaltungsnetz (unter „Kurswahlen“ prüfen, filtern, ändern, Abgänger markieren und drucken).",
            "7 · Kursplanung: Wahlzahlen ansehen; kommt ein Kurs nicht zustande, über das ✕ neben der Wahlzahl streichen. Nachwahl unter „Kurswahlen“ direkt eintragen oder Nachwahlbögen drucken.",
            "8 · ASV-Abgleich: Den ASV-BW-Export „zz Kursstufe Fächerwahl“ einlesen und auf Deckungsgleichheit mit dem Lehrerbestand prüfen." })
        {
            stepsCard.Children.Add(Text(step));
        }
        panel.Children.Add(Surface(stepsCard, 14));
        panel.Children.Add(Help("Was bedeuten die Fachbegriffe?",
            "LF – Leistungsfach (5-stündig, alle vier Halbjahre).\nBF – Basisfach (meist 2- oder 3-stündig).\nHJ 1–4 – die vier Halbjahre der Kursstufe; KS1 = HJ 1+2, KS2 = HJ 3+4.\n\n" +
            "Prüfstand einer Wahl:\n• Formal gültig – keine offenen Punkte.\n• Fachlich zu klären – z. B. Vorbildung unbekannt, Sportersatz, Religion/Ethik-Wechsel. Kein Fehler, aber eine Entscheidung der Oberstufenberatung ist nötig.\n• Regelverstoß – die Belegung widerspricht einer Regel (z. B. fehlendes Pflichtfach).\n\n" +
            "Vorbildung „Unbekannt“ bedeutet: Es liegt keine Angabe vor. Das Programm behandelt das weder als erfüllt noch als nicht erfüllt, sondern als zu klären.\n\n" +
            "Nachwahl – Eine Person muss nach einer Kursstreichung oder wegen eines Regelverstoßes erneut wählen.\n" +
            "Abgänger – Voraussichtliche Abgänger können in der Kursplanung ausgeblendet werden; bestätigte Abgänger zählen nie zur Planung. Ihre Wahl bleibt gespeichert.\n" +
            "Profil – kleine Datei mit den bestätigten Voraussetzungen einer Person (z. B. IMP, bilingual). Das Schülerprogramm zeigt damit die passenden Fächer an.\n" +
            "Revision – jeder gespeicherte Stand einer Wahl. Nichts wird überschrieben; Wiederherstellen legt einen neuen Stand an."));
        panel.Children.Add(Help("Verwaltungsnetz und pädagogisches Netz (Paket inkl. Programm & Transportdatei)",
            "Schülerordner sind meist nur im pädagogischen Netz erreichbar, dort ist das Lehrerprogramm im Allgemeinen nicht mehr vorhanden. Vor dem Wechsel ins pädagogische Netz im Verwaltungsnetz unter „Schülerdateien verteilen“ ein Paket inkl. Programm (.zip) erstellen (oder falls das Programm dort schon liegt, nur eine Transportdatei). Im pädagogischen Netz das ZIP im persönlichen Laufwerk (U:) entpacken, „Lehrerprogramm starten.cmd“ öffnen, den Transportstand übernehmen, verteilen und einsammeln.\n\nNach dem abschließenden Einsammeln unter „Kurswahlen einsammeln“ den „Stand für Verwaltungsnetz speichern …“ (.gsm-transfer) und im Verwaltungsnetz öffnen. Sobald die Transportdatei dort sicher angekommen und geöffnet ist, das Lehrerprogramm im pädagogischen Netz schließen und den gesamten entpackten Paketordner samt Arbeitsstand und ZIP-Datei im pädagogischen Netz vollständig löschen. Es gibt keine automatische Zusammenführung zweier Stände."));
        panel.Children.Add(Help("Dateiübersicht: Inhalt, Herkunft und Verwendung", "Schülerliste (.xlsx / .csv)\nEine Tabelle aus Ihrer vorbereiteten Klassen-10-Liste bzw. aus ASV-BW über das schuleigene Exportformat „GSM Export für Kurswahl“ (ASV-ID, Familienname, Rufname, Klasse, Klassengruppe, Profil, Unterrichtsfächer; wird automatisch erkannt und umgerechnet). Unter Wahl → Schülerliste importieren.\n\nFachangebot (.json), z. B. fachangebot-demo.json\nBeschreibt Fächer, Kursarten und Halbjahre eines Jahrgangs. Wird beim Anlegen eines Jahrgangs automatisch geladen und beim Verteilen in jeden Schülerordner kopiert.\n\nVerwaltungsdatei (.db), z. B. verwaltung.db\nSpeichert den Lehrerbestand mit Änderungen und Historie lokal. Wird automatisch angelegt und geöffnet.\n\nPersönliches Profil (profil.json)\nDas Lehrerprogramm erzeugt diese Datei aus den geprüften Voraussetzungen und legt sie beim Verteilen in den Wahlordner. Keine Schülerliste und keine Kurswahl.\n\nKurswahl (aktuell.json)\nWird vom Schülerprogramm gespeichert. Das Lehrerprogramm liest sie automatisch aus dem bestätigten Wahlordner.\n\nKontrollbogen (.pdf)\nLesbare Übersicht zur Kurswahl. Zum Ansehen und Prüfen (ohne Unterschriftenzeile); kein Importformat.\n\nTransportdatei (.gsm-transfer)\nKompletter Jahrgang für den Wechsel zwischen Verwaltungs- und pädagogischem Netz bzw. als Sicherung.\n\nASV-Kurswahl-Export („zz Kursstufe Fächerwahl“)\nUnter ASV-Abgleich als .xlsx oder .csv direkt einlesbar, um Deckungsgleichheit von Kursen, Halbjahren und geplanten mündlichen Prüfungsfächern zu prüfen."));
        panel.Children.Add(Help("Mit welchen Testdateien kann ich beginnen?", "Im Projektordner samples liegen:\n• fachangebot-demo.json: wird beim Anlegen eines Jahrgangs automatisch als Vorlage geladen.\n• klasse10-demo.csv: unter Schülerliste importieren laden; enthält zwei erfundene Personen.\n• schueler-demo.json: Beispiel für eine Kurswahl. Beim Einsammeln erwartet das Programm den Namen aktuell.json im bestätigten Wahlordner.\n• profil-demo-unsigned.json: Beispiel für ein persönliches Schülerprofil."));
        panel.Children.Add(Help("Nur für die Einrichtung: Programmeinstellungen", "appsettings.json im Schülerprogramm enthält die Programmeinstellungen (Speicherort, Katalogpfad). Diese Datei wird nicht als Schülerliste importiert."));
        return panel;
    }
    private static string Describe(StudentRecord r)
    {
        var p = r.Student.Prerequisites;
        return $"{r.Student.DisplayName}, Klasse {r.Student.ClassName}, Abitur {r.Student.GraduationYear}, Abgänger: {FormatDeparture(r.Student.Departure)}\n" +
            $"Konto: {(r.WindowsUsername.Length == 0 ? "–" : r.WindowsUsername)}; Wahlordner: {(r.HomeDirectory.Length == 0 ? "–" : r.HomeDirectory)}\n" +
            $"Italienisch-Profil: {FormatKnowledge(p.ItalianProfile)}; Informatik-Brückenkurs: {FormatKnowledge(p.InformaticsBridge)}; IMP: {FormatKnowledge(p.InformaticsImp)}; durchgängig bilingual: {FormatKnowledge(p.ContinuousBilingual)}\n" +
            $"Religion/Ethik Klasse 10: {p.ReligionEthicsGrade10 ?? "unbekannt"}; Belegung freigegeben: {FormatKnowledge(p.ReligionEthicsPermission)}; Prüfung freigegeben: {FormatKnowledge(p.ReligionEthicsExamPermission)}\n" +
            $"Sportbefreiung: {(p.SportExemptSemesters is { } h ? (h.Length == 0 ? "keine" : "HJ " + string.Join(',', h)) : "unbekannt")}; dauerhaft: {FormatKnowledge(p.PermanentSportExemption)}; fortgeführte Sprachen: {(p.ContinuedLanguages.IsEmpty ? "–" : string.Join(',', p.ContinuedLanguages))}";
    }
    private async Task<string?> Pick(string title, string typeName, params string[] patterns)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = false, FileTypeFilter = [new FilePickerFileType(typeName) { Patterns = patterns }] });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
    private void Ready() { if (store is null || session is null) throw new InvalidOperationException("Bitte zuerst einen Jahrgang einrichten oder auswählen."); }
    private void Refresh(bool preserveEditor = false)
    {
        if (session is null || store is null) return;
        roster.Children.Clear();
        roster.Children.Add(new Border { Background = SoftAccent, Child = TableLine("Name", "Klasse", "Prüfstand", "Hinweise", Text("")) });
        var states = session.Overview().Where(s => s.Record.Student.GraduationYear == session.Catalog.GraduationYear).ToArray();
        var shownCount = 0;
        foreach (var state in states)
        {
            var label = $"{state.Record.Student.DisplayName} · {state.Record.Student.ClassName} · {state.Record.Student.ExternalStudentId}";
            if (!label.Contains(filter.Text ?? "", StringComparison.OrdinalIgnoreCase) || followupFilter.IsChecked == true && !state.RequiresReselection) continue;
            if (statusFilter.SelectedIndex == 1 && state.Active is not null || statusFilter.SelectedIndex == 2 && state.Validation?.Status != ReviewStatus.Invalid
                || statusFilter.SelectedIndex == 3 && state.Validation?.Status != ReviewStatus.NeedsReview || statusFilter.SelectedIndex == 4 && state.Validation?.Status != ReviewStatus.Valid) continue;
            var religion = ReligionCases.For(state);
            if (statusFilter.SelectedIndex == 5 && religion is null || statusFilter.SelectedIndex == 6 && religion?.AssessmentSubject is null) continue;
            shownCount++;
            var notes = string.Join(" · ", new[] { state.RequiresReselection ? "Nachwahl offen" : "", religion?.Note ?? "", state.Record.Student.Departure == DepartureStatus.Active ? "" : "Abgänger: " + FormatDeparture(state.Record.Student.Departure), state.Record.MappingConfirmed ? "" : "Ordner nicht bestätigt" }.Where(s => s.Length > 0));
            var open = Compact(Button("Öffnen", () => ShowStudent(state), "Stammdaten, Vorbildung, Kurswahl und Historie dieser Person rechts anzeigen."));
            var selected = state.Record.Student.ExternalStudentId == selectedStudentId && personEditor.IsExpanded;
            roster.Children.Add(new Border { Background = selected ? HighlightAccent : Brushes.Transparent, BorderBrush = selected ? Accent : Brushes.Transparent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Child = TableLine(state.Record.Student.DisplayName, state.Record.Student.ClassName, FormatStatus(state.Validation?.Status), notes, open) });
        }
        if (shownCount == 0)
            roster.Children.Add(new TextBlock { Text = "Keine Person passt zu diesen Filtern.", Foreground = Muted, Margin = new Thickness(8, 12) });
        rosterCount.Text = $"{shownCount} von {states.Length} Personen";
        rosterResetFilter.IsEnabled = !string.IsNullOrWhiteSpace(filter.Text) || followupFilter.IsChecked == true || statusFilter.SelectedIndex > 0;
        if (!preserveEditor)
        {
            profileTargets.ItemsSource = store.Students().Where(s => s.Student.GraduationYear == session.Catalog.GraduationYear)
                .OrderBy(s => s.Student.ClassName, StringComparer.CurrentCulture)
                .ThenBy(s => s.Student.SortName().Last, StringComparer.CurrentCulture)
                .ThenBy(s => s.Student.SortName().First, StringComparer.CurrentCulture)
                .ThenBy(s => s.Student.ExternalStudentId, StringComparer.Ordinal)
                .Select(s => new Target(s)).ToArray();
            profileTargets.SelectAll();
        }
        if (!preserveEditor && personEditor.IsExpanded && selectedStudentId is not null && states.SingleOrDefault(s => s.Record.Student.ExternalStudentId == selectedStudentId) is { } current)
            ShowStudent(current);
    }
    /// <summary>Opens the people page with one person selected (used by the results table).</summary>
    private void OpenStudent(string studentId)
    {
        Ready();
        ShowPage("people");
        var state = session!.Overview().SingleOrDefault(s => s.Record.Student.ExternalStudentId == studentId) ?? throw new InvalidOperationException("Person nicht gefunden.");
        ShowStudent(state);
        Refresh(preserveEditor: true);
    }
    private void ShowStudent(TeacherState state)
    {
        Ready();
        selectedStudentId = state.Record.Student.ExternalStudentId; personEditor.IsExpanded = true; detail.Children.Clear();
        var record = state.Record; var id = record.Student.ExternalStudentId;
        personEditor.Header = $"{record.Student.DisplayName} · Klasse {record.Student.ClassName}";
        var summaryRow = new WrapPanel { Orientation = Orientation.Horizontal };
        var summary = Text($"Schüler-ID {id} · Prüfstand: {FormatStatus(state.Validation?.Status)}" + (state.RequiresReselection ? " · Nachwahl offen" : "") + (state.Record.Student.Departure == DepartureStatus.Active ? "" : " · Abgänger: " + FormatDeparture(state.Record.Student.Departure)));
        summary.Foreground = Muted; summary.Margin = new Thickness(0, 0, 12, 6); summary.VerticalAlignment = VerticalAlignment.Center;
        summaryRow.Children.Add(summary);
        var openInGrid = Compact(Button("Kurswahl im Raster bearbeiten →", () => { choiceSelectedId = id; ShowResults(); },
            "Öffnet diese Person direkt in der komfortablen Halbjahresansicht unter „Kurswahlen“."));
        openInGrid.Name = "OpenInChoiceGrid";
        summaryRow.Children.Add(openInGrid);
        detail.Children.Add(summaryRow);

        // Findings directly at the top so rule violations and review items are seen without scrolling.
        if (state.Validation is { } validation && validation.Messages.Length > 0)
        {
            var invalidItems = validation.Messages.Where(m => m.Rule.Severity == ReviewStatus.Invalid).ToArray();
            var reviewItems = validation.Messages.Where(m => m.Rule.Severity != ReviewStatus.Invalid).ToArray();
            if (invalidItems.Length > 0)
            {
                var box = new StackPanel { Spacing = 3 };
                box.Children.Add(new TextBlock { Text = $"Regelverstöße ({invalidItems.Length})", FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#A32D2D") });
                foreach (var m in invalidItems)
                {
                    var line = Text("✗ " + m.Explanation); line.Foreground = Brush.Parse("#8C1D1D");
                    ToolTip.SetTip(line, $"{m.Rule.Description}\nAbhilfe: {m.Remedy}\nRegel {m.Rule.Id} · Quelle: {m.Rule.Source}");
                    box.Children.Add(line);
                }
                detail.Children.Add(new Border { Background = Brush.Parse("#FCEBEB"), BorderBrush = Brush.Parse("#A32D2D"), BorderThickness = new Thickness(4, 1, 1, 1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6), Child = box });
            }
            if (reviewItems.Length > 0)
            {
                var box = new StackPanel { Spacing = 3 };
                box.Children.Add(new TextBlock { Text = $"Fachlich zu klären ({reviewItems.Length})", FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#854F0B") });
                foreach (var m in reviewItems)
                {
                    var line = Text("• " + m.Explanation);
                    ToolTip.SetTip(line, $"{m.Rule.Description}\nAbhilfe: {m.Remedy}\nRegel {m.Rule.Id} · Quelle: {m.Rule.Source}");
                    box.Children.Add(line);
                }
                detail.Children.Add(new Border { Background = Brush.Parse("#FAEEDA"), BorderBrush = Brush.Parse("#C48723"), BorderThickness = new Thickness(4, 1, 1, 1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6), Child = box });
            }
        }
        else if (state.Active is null)
            detail.Children.Add(new TextBlock { Text = "Für diese Person wurde noch keine Kurswahl eingelesen. Sie können unten oder im Kurswahlraster eine Wahl im Namen der Beratung erfassen.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 13 });

        // ---- Section 1: Prerequisites and departure
        var prereqTab = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4) };
        var p = record.Student.Prerequisites;
        prereqTab.Children.Add(new TextBlock { Text = "„Unbekannt“ heißt: keine Angabe. Das Programm wertet das als „fachlich zu klären“, nicht als erfüllt oder nicht erfüllt. Fahren Sie mit der Maus über ⓘ für Erklärungen.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        ComboBox KnowledgeBox(string name, Knowledge value) => EnumBox(name, value, FormatKnowledge, 160);
        var departureBox = EnumBox("StudentDeparture", record.Student.Departure, FormatDeparture, 220);
        var italianBox = KnowledgeBox("PrereqItalian", p.ItalianProfile);
        var bridgeBox = KnowledgeBox("PrereqBridge", p.InformaticsBridge);
        var impBox = KnowledgeBox("PrereqImp", p.InformaticsImp);
        var biliBox = KnowledgeBox("PrereqBilingual", p.ContinuousBilingual);
        var relItems = new[] { new SubjectItem(null, "Unbekannt"), new SubjectItem("ETH", "Ethik"), new SubjectItem("ER", "Ev. Religion"), new SubjectItem("KR", "Kath. Religion") };
        var relGrade10Box = new ComboBox { Name = "PrereqReligionGrade10", ItemsSource = relItems, SelectedItem = relItems.FirstOrDefault(i => i.Id == p.ReligionEthicsGrade10) ?? relItems[0], Width = 160 };
        var relPermBox = KnowledgeBox("PrereqReligionPerm", p.ReligionEthicsPermission);
        var relExamPermBox = KnowledgeBox("PrereqReligionExamPerm", p.ReligionEthicsExamPermission);
        var sportBox = new TextBox { Name = "PrereqSportSemesters", Text = p.SportExemptSemesters is { } h ? (h.Length == 0 ? "keine" : string.Join(", ", h)) : "", Watermark = "leer = unbekannt, „keine“, oder z. B. 1, 2", Width = 260 };
        ToolTip.SetTip(sportBox, "Halbjahre mit Sportbefreiung (1 bis 4). „keine“ bedeutet bestätigt ohne Befreiung. Leer bedeutet unbekannt. Für jedes befreite Halbjahr ist ein zusätzlicher Kurs nötig.");
        var permSportBox = KnowledgeBox("PrereqPermanentSport", p.PermanentSportExemption);
        var langBox = new TextBox { Name = "PrereqLanguages", Text = string.Join(", ", p.ContinuedLanguages), Watermark = "Fachkürzel z. B. E, F, L", Width = 260 };
        ToolTip.SetTip(langBox, "Fachkürzel der fortgeführten Fremdsprachen, getrennt durch Komma (z. B. E, F, L). Bestätigt die Sprachlaufbahn.");
        var prereqErrorBox = InlineErrorBox("StudentPrereqError", out var prereqErrorText);
        sportBox.TextChanged += (_, _) => { sportBox.ClearValue(BorderBrushProperty); SetInlineError(prereqErrorBox, prereqErrorText, null); };

        prereqTab.Children.Add(Labeled("Abgängerstatus", departureBox, "Aktiv: zählt normal. Voraussichtlich: kann in der Kursplanung ausgeblendet werden. Bestätigt: zählt nie zur Planung. Die Wahl bleibt in jedem Fall gespeichert."));
        prereqTab.Children.Add(Labeled("Italienisch-Profil (3. Fremdsprache)", italianBox, "Italienisch als dritte Fremdsprache im Profil der Klasse 10. Schaltet BF Italienisch frei. Ob Italienisch als fortgeführte Fremdsprache zählt, wird unten bei den Sprachen gesondert bestätigt."));
        prereqTab.Children.Add(Labeled("Informatik-Brückenkurs Klasse 10", bridgeBox, "Brückenkurs oder IMP schaltet BF Informatik frei. Beide Wege werden getrennt erfasst."));
        prereqTab.Children.Add(Labeled("IMP-Profil besucht", impBox, "Profil Informatik-Mathematik-Physik. Brückenkurs oder IMP schaltet BF Informatik frei."));
        prereqTab.Children.Add(Labeled("Durchgängig bilingual", biliBox, "Durchgängiger bilingualer Bildungsgang. Voraussetzung für Biologie bilingual (LF und BF)."));
        prereqTab.Children.Add(Labeled("Religion/Ethik in Klasse 10", relGrade10Box, "Bestimmt die angebotene Religion: ER erlaubt evangelische, KR katholische Religion; bei ETH ist keine Religion wählbar. Bei unbekanntem Fach bleiben beide Religionsfächer vorläufig wählbar und werden als Klärfall gemeldet. Ethik bleibt wählbar. Wechsel können die mündliche Prüfung einschränken."));
        prereqTab.Children.Add(Labeled("Religion/Ethik: Belegung freigegeben", relPermBox, "Dokumentiert eine ggf. nötige Zustimmung. Gibt keine Religion außerhalb des bestätigten Klasse-10-Fachs frei."));
        prereqTab.Children.Add(Labeled("Religion/Ethik: Prüfung freigegeben", relExamPermBox, "Ist die mündliche Prüfung im gewählten Fach trotz Wechsel zugelassen, z. B. nach bestandener Feststellungsprüfung?"));
        prereqTab.Children.Add(Labeled("Sportbefreiung", sportBox, "Halbjahre mit Sportbefreiung. Für jedes befreite Halbjahr ist ein zusätzlicher Basis- oder Wahlfachkurs nötig; der Seminarkurs zählt nicht. Keine Diagnosen eintragen."));
        prereqTab.Children.Add(Labeled("Dauerhafte Sportbefreiung", permSportBox, "Dauerhafte, auch teilweise Befreiung zum Wahlzeitpunkt. Schließt LF Sport als normale Wahl aus."));
        prereqTab.Children.Add(Labeled("Fortgeführte Fremdsprachen", langBox, "Sprachen mit bestätigter Sprachlaufbahn (z. B. E, F, L). Englisch, Französisch und Latein zählen ohnehin; wichtig ist die Bestätigung vor allem für Italienisch und weitere Sprachen."));
        prereqTab.Children.Add(prereqErrorBox);
        var savePrereq = Button("Vorbildung und Abgängerstatus speichern", () =>
        {
            try
            {
                var sportText = (sportBox.Text ?? "").Trim().ToLowerInvariant();
                ImmutableArray<int>? semesters;
                if (sportText is "" or "unknown" or "unbekannt")
                    semesters = null;
                else if (sportText is "keine" or "none")
                    semesters = [];
                else
                {
                    var parsed = new List<int>();
                    foreach (var part in sportText.Split(',', StringSplitOptions.TrimEntries))
                    {
                        if (!int.TryParse(part, System.Globalization.CultureInfo.InvariantCulture, out var sem) || sem is < 1 or > 4)
                        {
                            sportBox.BorderBrush = Brush.Parse("#A32D2D"); sportBox.Focus();
                            throw new ArgumentException("Sportbefreiung muss Halbjahre 1 bis 4 (z. B. 1, 2), „keine“ oder leer enthalten.");
                        }
                        parsed.Add(sem);
                    }
                    if (parsed.Distinct().Count() != parsed.Count)
                    {
                        sportBox.BorderBrush = Brush.Parse("#A32D2D"); sportBox.Focus();
                        throw new ArgumentException("Sportbefreiung muss verschiedene Halbjahre 1 bis 4 als Zahlen enthalten.");
                    }
                    semesters = [.. parsed.Order()];
                }
                var updatedPrior = new PriorEducation((Knowledge)italianBox.SelectedItem!, (Knowledge)bridgeBox.SelectedItem!, (Knowledge)impBox.SelectedItem!, (Knowledge)biliBox.SelectedItem!, ((SubjectItem)relGrade10Box.SelectedItem!).Id, semesters)
                {
                    ContinuedLanguages = [.. (langBox.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase)],
                    ReligionEthicsPermission = (Knowledge)relPermBox.SelectedItem!,
                    ReligionEthicsExamPermission = (Knowledge)relExamPermBox.SelectedItem!,
                    PermanentSportExemption = (Knowledge)permSportBox.SelectedItem!
                };
                store!.SaveStudent(record with
                {
                    Student = record.Student with { Departure = (DepartureStatus)departureBox.SelectedItem!, Prerequisites = updatedPrior },
                    Version = record.Version + 1, Actor = Actor, ChangedAt = DateTimeOffset.UtcNow
                }, record.Version);
                Refresh(); SetStatus("Vorbildung und Abgängerstatus gespeichert. Bei geänderter Vorbildung danach Schülerdateien erneut verteilen.", FeedbackKind.Success);
            }
            catch (Exception ex) { SetInlineError(prereqErrorBox, prereqErrorText, ex.Message); throw; }
        }, "Speichert als neuen Stand. Geänderte Vorbildung erfordert eine neue Verteilung der Schülerdateien, damit das Schülerprogramm sie kennt.");
        savePrereq.Name = "SavePrerequisites"; prereqTab.Children.Add(Primary(savePrereq));
        detail.Children.Add(new Expander
        {
            Name = "StudentPrereqExpander", Header = "Vorbildung und Abgängerstatus", IsExpanded = true, HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = Surface(prereqTab, 14)
        });

        // ---- Section 2: account and folder
        var folderTab = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4) };
        var user = new TextBox { Name = "StudentUsername", Text = record.WindowsUsername, Watermark = "DOMÄNE\\Benutzername (optional)" };
        var home = new TextBox { Name = "StudentHomeDirectory", Text = record.HomeDirectory, Watermark = "Persönlicher Wahlordner" };
        var confirmed = new CheckBox { Name = "StudentMappingConfirmed", Content = "Ich habe geprüft, dass dieser Ordner wirklich zu dieser Person gehört", IsChecked = record.MappingConfirmed };
        var folderErrorBox = InlineErrorBox("StudentFolderError", out var folderErrorText);
        user.TextChanged += (_, _) => SetInlineError(folderErrorBox, folderErrorText, null);
        home.TextChanged += (_, _) => SetInlineError(folderErrorBox, folderErrorText, null);
        confirmed.IsCheckedChanged += (_, _) => SetInlineError(folderErrorBox, folderErrorText, null);
        folderTab.Children.Add(Labeled("Windows-Konto", user, "Optionales schulisches Windows-Konto der Person in der Form DOMÄNE\\Benutzername."));
        folderTab.Children.Add(Labeled("Wahlordner", home, "Persönlicher Ordner der Person. Das Lehrerprogramm legt darin beim Verteilen den Unterordner „" + StudentFolderLayout.RootFolder + "“ an (Startdatei, Programm, Daten mit Profil, Fachangebot und Kurswahl) und entfernt ihn beim abschließenden Einsammeln wieder. Vor der ersten Verteilung genügt der relative Ordner Klasse/Nachname.Vorname; bei der Verteilung wird er zum vollständigen Pfad."));
        ToolTip.SetTip(confirmed, "Nur bestätigte Ordner werden beim Einsammeln gelesen. Bei der gemeinsamen Verteilung wird die Bestätigung automatisch gesetzt.");
        folderTab.Children.Add(confirmed);
        folderTab.Children.Add(folderErrorBox);
        folderTab.Children.Add(Button("Zuordnung speichern", () =>
        {
            try
            {
                var trimmedUser = (user.Text ?? "").Trim();
                var trimmedHome = (home.Text ?? "").Trim();
                var userParts = trimmedUser.Split('\\');
                if (trimmedUser.Length > 0 && (userParts.Length != 2 || userParts.Any(string.IsNullOrWhiteSpace)))
                    throw new ArgumentException("Windows-Konto muss die Form DOMÄNE\\Benutzername haben.");
                string normalizedHome;
                if (trimmedHome.Length == 0) normalizedHome = "";
                else if (Path.IsPathFullyQualified(trimmedHome)) normalizedHome = Path.GetFullPath(trimmedHome);
                else if (!Path.IsPathRooted(trimmedHome) && !trimmedHome.Split('/', '\\').Any(p => p is ".." or "." or "")) normalizedHome = trimmedHome.Replace('\\', '/');
                else throw new ArgumentException("Ungültiger Wahlordner.");
                if (confirmed.IsChecked == true)
                {
                    if (!Path.IsPathFullyQualified(normalizedHome) || !Directory.Exists(normalizedHome))
                        throw new ArgumentException("Für eine bestätigte Zuordnung muss der Ordner als vollständiger Pfad existieren.");
                    for (var directory = new DirectoryInfo(normalizedHome); directory is not null; directory = directory.Parent)
                        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                            throw new ArgumentException("Verknüpfte Ordner bitte durch einen direkten Pfad ersetzen.");
                }
                store!.SaveStudent(record with { WindowsUsername = trimmedUser, HomeDirectory = normalizedHome, MappingConfirmed = confirmed.IsChecked == true, Version = record.Version + 1, Actor = Actor, ChangedAt = DateTimeOffset.UtcNow }, record.Version);
                Refresh(); SetStatus("Zuordnung gespeichert.", FeedbackKind.Success);
            }
            catch (Exception ex) { SetInlineError(folderErrorBox, folderErrorText, ex.Message); throw; }
        }, "Speichert Konto und Wahlordner als neuen Stand. Frühere Stände bleiben in der Historie."));
        detail.Children.Add(new Expander
        {
            Name = "StudentFolderExpander",
            Header = $"Konto und Wahlordner · {(record.MappingConfirmed ? "bestätigt" : "nicht bestätigt")}",
            IsExpanded = !record.MappingConfirmed,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = Surface(folderTab, 14)
        });

        // ---- Section 3: follow-up and teacher edit
        var choiceTab = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4) };
        choiceTab.Children.Add(new TextBlock { Text = "Tipp: Für die schnelle Bearbeitung im Halbjahresraster oben auf „Kurswahl im Raster bearbeiten →“ klicken.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        choiceTab.Children.Add(Heading("Nachwahl"));
        if (!string.IsNullOrWhiteSpace(state.Note)) choiceTab.Children.Add(Text("Vermerk: " + state.Note));
        var editReason = new TextBox { Name = "StudentEditReason", Watermark = "Begründung für Nachwahl oder Änderung (Pflicht für das Änderungsprotokoll)" };
        var choiceErrorBox = InlineErrorBox("StudentChoiceError", out var choiceErrorText);
        editReason.TextChanged += (_, _) => { editReason.ClearValue(BorderBrushProperty); SetInlineError(choiceErrorBox, choiceErrorText, null); };
        choiceTab.Children.Add(editReason);
        var deadline = new CalendarDatePicker { Name = "ReselectionDeadline", SelectedDate = state.ReselectionDeadline?.LocalDateTime, Watermark = "Nachwahlfrist (optional)" };
        var followRow = new WrapPanel { Orientation = Orientation.Horizontal };
        deadline.Margin = new Thickness(0, 0, 10, 6); followRow.Children.Add(deadline);
        var mark = Button("Nachwahl vormerken", () =>
        {
            try
            {
                session!.FollowUp(id, true, deadline.SelectedDate is { } d ? new DateTimeOffset(d.Date) : null, editReason.Text ?? "");
                Refresh(); SetStatus("Nachwahl vorgemerkt und protokolliert.", FeedbackKind.Success);
            }
            catch (Exception ex) { SetInlineError(choiceErrorBox, choiceErrorText, ex.Message); if (string.IsNullOrWhiteSpace(editReason.Text)) { editReason.BorderBrush = Brush.Parse("#A32D2D"); editReason.Focus(); } throw; }
        }, "Markiert die Person als „Nachwahl offen“. Erscheint im Filter, auf der Übersicht und unter „Kurswahlen“.");
        mark.Margin = new Thickness(0, 0, 10, 6); followRow.Children.Add(mark);
        followRow.Children.Add(Button("Nachwahl abschließen", () =>
        {
            try
            {
                session!.FollowUp(id, false, null, editReason.Text ?? "");
                Refresh(); SetStatus("Nachwahl abgeschlossen.", FeedbackKind.Success);
            }
            catch (Exception ex) { SetInlineError(choiceErrorBox, choiceErrorText, ex.Message); if (string.IsNullOrWhiteSpace(editReason.Text)) { editReason.BorderBrush = Brush.Parse("#A32D2D"); editReason.Focus(); } throw; }
        }, "Beendet die offene Nachwahl; der Vorgang bleibt im Protokoll."));
        choiceTab.Children.Add(followRow);

        var active = state.Active;
        var choice = active?.Snapshot ?? new CourseChoice(2, Guid.NewGuid(), id, record.Student.GraduationYear, session!.Catalog.Version, GsmKurswahl.Rules.ChoiceValidator.Version, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, SubmissionStatus.Draft, ElectionPhase.Preliminary, [], []);
        choiceTab.Children.Add(Heading("Kurswahl durch die Beratung ändern"));
        choiceTab.Children.Add(new TextBlock { Text = "Haken je Halbjahr setzen oder entfernen. Die ursprüngliche Schülerwahl bleibt in der Historie erhalten.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var selections = new List<(CourseOffer Offer, int Semester, CheckBox Box)>();
        var profile = new ProfileContext(ProfileStatus.Verified, record.Student.Prerequisites);
        GsmKurswahl.Rules.Availability? ReligionAvailability(CourseOffer offer) => offer.SubjectId is "ER" or "KR"
            ? new GsmKurswahl.Rules.ChoiceValidator().GetAvailability(offer, profile) : null;
        var extraOffers = choice.Selections.Where(s => session!.Catalog.Offers.All(o => o.Id != s.OfferId))
            .Select(s => new CourseOffer(s.OfferId, s.SubjectId, s.Type, s.Variant, OfferStatus.Cancelled,
                [.. s.Semesters.Order().Select(h => new SemesterHours(h, 0))], []))
            .ToArray();
        foreach (var offer in session!.Catalog.Offers.Concat(extraOffers))
        {
            var inCatalog = session.Catalog.Offers.Any(o => o.Id == offer.Id);
            var isOffered = inCatalog && offer.Status is OfferStatus.Offered or OfferStatus.Established;
            var selectedSelection = choice.Selections.FirstOrDefault(s => s.OfferId == offer.Id);
            var religion = ReligionAvailability(offer);
            if ((!isOffered || religion?.CanSelect == false) && selectedSelection is null) continue;
            var scheduled = inCatalog ? offer.Schedule.Select(s => s.Semester).ToHashSet() : new HashSet<int>();
            var allSemesters = scheduled.Concat(selectedSelection?.Semesters ?? []).Distinct().Order().ToArray();
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var labelText = inCatalog
                ? OfferLabel(offer.Id) + (isOffered ? "" : $" ({FormatOfferStatus(offer.Status)})")
                : $"{SubjectName(offer.SubjectId)} {FormatCourseType(offer.Type)}{(offer.Variant == CourseVariant.Bilingual ? " bilingual" : "")} ({offer.Id}, unbekanntes Angebot)";
            line.Children.Add(new TextBlock { Text = labelText, Width = 240, VerticalAlignment = VerticalAlignment.Center, Foreground = isOffered ? Ink : Brush.Parse("#8C1D1D") });
            if (religion is not null) ToolTip.SetTip(line, religion.Explanation);
            foreach (var semester in allSemesters)
            {
                var isSelected = selectedSelection?.Semesters.Contains(semester) == true;
                var allowed = inCatalog && scheduled.Contains(semester) && religion?.CanSelect != false;
                var box = new CheckBox { Name = $"TeacherChoice_{offer.Id.Replace('-', '_')}_{semester}", Content = "HJ " + semester, IsChecked = isSelected, IsEnabled = allowed || isSelected };
                if (!allowed)
                    box.IsCheckedChanged += (_, _) => { if (box.IsChecked != true) box.IsEnabled = false; };
                line.Children.Add(box); selections.Add((offer, semester, box));
            }
            choiceTab.Children.Add(line);
        }
        var oralItems = new[] { new SubjectItem(null, "— nicht geplant —") }.Concat(session.Catalog.Offers.Where(o => o.Type == CourseType.Bf).Select(o => o.SubjectId).Distinct()
            .Select(s => new SubjectItem(s, SubjectName(s)))).ToArray();
        ComboBox OralBox(string name, string? current)
        {
            var baseItems = oralItems.ToList();
            if (!string.IsNullOrWhiteSpace(current) && baseItems.All(i => i.Id != current))
                baseItems.Add(new SubjectItem(current, SubjectName(current)));
            var items = baseItems.Select(i => (Item: i, Availability: session.Catalog.Offers.Where(o => o.SubjectId == i.Id && o.Type == CourseType.Bf)
                    .Select(ReligionAvailability).FirstOrDefault(), InOralItems: oralItems.Any(o => o.Id == i.Id)))
                .Where(x => x.Availability?.CanSelect != false || x.Item.Id == current)
                .Select(x => new ComboBoxItem { Content = x.Item.Label, Tag = x.Item, IsEnabled = x.InOralItems && x.Availability?.CanSelect != false }).ToArray();
            return new() { Name = name, ItemsSource = items, SelectedItem = items.FirstOrDefault(i => ((SubjectItem)i.Tag!).Id == current) ?? items[0], Width = 220, Margin = new Thickness(0, 0, 10, 0) };
        }
        var oral1 = OralBox("OralSubject1", choice.PlannedOralSubjects.ElementAtOrDefault(0));
        var oral2 = OralBox("OralSubject2", choice.PlannedOralSubjects.ElementAtOrDefault(1));
        var oralRow = new WrapPanel { Orientation = Orientation.Horizontal }; oralRow.Children.Add(oral1); oralRow.Children.Add(oral2);
        choiceTab.Children.Add(Labeled("Geplante mündliche Prüfungsfächer", oralRow, "Unverbindliche Planung der zwei mündlichen Prüfungsfächer. Zusammen mit den drei Leistungsfächern müssen Deutsch, Mathematik und alle drei Aufgabenfelder abgedeckt sein."));
        choiceTab.Children.Add(choiceErrorBox);
        var saveChoice = Button("Änderung als neuen Stand speichern", () =>
        {
            try
            {
                var selected = selections.Where(s => s.Box.IsChecked == true).GroupBy(s => s.Offer).Select(g => new CourseSelection(g.Key.Id, g.Key.SubjectId, g.Key.Type, g.Key.Variant, [.. g.Select(v => v.Semester)])).ToImmutableArray();
                var oral = new[] { ((SubjectItem)((ComboBoxItem)oral1.SelectedItem!).Tag!).Id, ((SubjectItem)((ComboBoxItem)oral2.SelectedItem!).Tag!).Id }.OfType<string>().Distinct().ToImmutableArray();
                session.Save(choice with { OfferVersion = session.Catalog.Version, RuleVersion = GsmKurswahl.Rules.ChoiceValidator.Version, Revision = active is null ? 1 : active.Snapshot.Revision + 1, Selections = selected, PlannedOralSubjects = oral, UpdatedAt = DateTimeOffset.UtcNow }, active?.Id, RevisionSource.TeacherEdit, editReason.Text ?? "");
                Refresh(); SetStatus("Lehreränderung gespeichert und gegen die aktuellen Stammdaten geprüft.", FeedbackKind.Success);
            }
            catch (Exception ex) { SetInlineError(choiceErrorBox, choiceErrorText, ex.Message); if (string.IsNullOrWhiteSpace(editReason.Text)) { editReason.BorderBrush = Brush.Parse("#A32D2D"); editReason.Focus(); } throw; }
        }, "Speichert die geänderte Belegung als neuen Stand mit Begründung. Die Begründung oben ist Pflicht.");
        saveChoice.Name = "SaveTeacherChoice"; choiceTab.Children.Add(Primary(saveChoice));
        detail.Children.Add(new Expander
        {
            Name = "StudentChoiceExpander", Header = "Nachwahl und direkte Kurswahlbearbeitung", IsExpanded = state.RequiresReselection, HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = Surface(choiceTab, 14)
        });

        // ---- Section 4: history
        var historyTab = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4) };
        historyTab.Children.Add(new TextBlock { Text = "Jeder Stand ist vollständig gespeichert. „Wiederherstellen“ legt einen neuen Stand an; nichts wird gelöscht.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var restoreReason = new TextBox { Name = "RestoreReason", Watermark = "Begründung für Wiederherstellung (Pflicht)" };
        var restoreErrorBox = InlineErrorBox("StudentRestoreError", out var restoreErrorText);
        restoreReason.TextChanged += (_, _) => { restoreReason.ClearValue(BorderBrushProperty); SetInlineError(restoreErrorBox, restoreErrorText, null); };
        var history = store!.History(id);
        if (history.IsEmpty) historyTab.Children.Add(Text("Noch keine gespeicherten Stände."));
        else
        {
            historyTab.Children.Add(restoreReason);
            historyTab.Children.Add(restoreErrorBox);
        }
        foreach (var revision in history.Reverse())
        {
            var diff = string.Join("\n", revision.Changes.Select(c => $"   {OfferLabel(c.OfferId)}: {Semesters(c.Before)} → {Semesters(c.After)}"));
            var oralSummary = revision.Snapshot.PlannedOralSubjects.IsEmpty ? "keine" : string.Join(", ", revision.Snapshot.PlannedOralSubjects.Select(SubjectName));
            var card = new StackPanel { Spacing = 4 };
            card.Children.Add(new TextBlock { Text = $"{revision.ChangedAt.LocalDateTime:g} · {FormatRevisionSource(revision.Source)} · {revision.ActorId} ({FormatActorRole(revision.ActorRole)})", FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            card.Children.Add(Text($"Begründung: {revision.Reason}\nMündlich geplant: {oralSummary}" + (diff.Length > 0 ? "\nÄnderungen:\n" + diff : "")));
            if (active is not null && revision.Id != active.Id)
                card.Children.Add(Button("Diesen Stand wiederherstellen", () =>
                {
                    var reasonText = !string.IsNullOrWhiteSpace(restoreReason.Text) ? restoreReason.Text! : editReason.Text ?? "";
                    try
                    {
                        session.Restore(id, revision.Id, active.Id, reasonText);
                        Refresh(); SetStatus("Früherer Stand als neuer Stand wiederhergestellt.", FeedbackKind.Success);
                    }
                    catch (Exception ex) { SetInlineError(restoreErrorBox, restoreErrorText, ex.Message); if (string.IsNullOrWhiteSpace(reasonText)) { restoreReason.BorderBrush = Brush.Parse("#A32D2D"); restoreReason.Focus(); } throw; }
                }, "Übernimmt diesen früheren Stand als neue, aktuelle Wahl. Begründung oben eintragen."));
            historyTab.Children.Add(SubtleCard(card, 10));
        }
        detail.Children.Add(new Expander
        {
            Name = "StudentHistoryExpander",
            Header = $"Änderungshistorie ({history.Length} {(history.Length == 1 ? "Stand" : "Stände")})",
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = Surface(historyTab, 14)
        });

        // ---- Section 5: Deletion (deliberate and irreversible, collapsed danger zone)
        var deletion = new StackPanel { Spacing = 8 };
        deletion.Children.Add(Text("Entfernt diese Person vollständig aus dem Lehrerbestand dieses Jahrgangs: Stammdaten, Vorbildung, alle Kurswahlstände, Nachwahl- und Übertragungsvermerke, Profile, Zustellberichte, archivierte Schülerdateien und die Person in gespeicherten Planungsständen. Zurück bleibt nur ein Protokolleintrag ohne Name und Schüler-ID. Dateien im Wahlordner werden nicht verändert. Vorher ggf. „Jahrgang sichern“."));
        var deleteReason = new TextBox { Name = "DeleteStudentReason", Watermark = "Löschgrund (z. B. Schulwechsel, Löschfrist erreicht)" };
        var deleteConfirm = new CheckBox { Name = "DeleteStudentConfirm", Content = $"Ich möchte {record.Student.DisplayName} ({id}) endgültig löschen" };
        var deleteErrorBox = InlineErrorBox("StudentDeleteError", out var deleteErrorText);
        deleteReason.TextChanged += (_, _) => SetInlineError(deleteErrorBox, deleteErrorText, null);
        deleteConfirm.IsCheckedChanged += (_, _) => SetInlineError(deleteErrorBox, deleteErrorText, null);
        deletion.Children.Add(deleteReason); deletion.Children.Add(deleteConfirm); deletion.Children.Add(deleteErrorBox);
        var deleteButton = Danger(Button("Person endgültig löschen", () =>
        {
            try
            {
                if (deleteConfirm.IsChecked != true) throw new InvalidOperationException("Bitte die Löschung ausdrücklich bestätigen.");
                if (store is not SqliteAdministrationStore sql) throw new InvalidOperationException("Löschen ist nur im lokalen Lehrerbestand möglich.");
                var log = sql.DeleteStudent(id, record.Student.Id, Actor, deleteReason.Text ?? "");
                detail.Children.Clear(); Refresh();
                SetStatus($"Person gelöscht ({log.RemovedRows} Datensätze, {log.UpdatedPlanningSnapshots} Planungsstände bereinigt).", FeedbackKind.Success);
            }
            catch (Exception ex) { SetInlineError(deleteErrorBox, deleteErrorText, ex.Message); throw; }
        }, "Nicht umkehrbar. Eine zuvor erstellte Jahrgangssicherung enthält die Person weiterhin."));
        deleteButton.Name = "DeleteStudent"; deletion.Children.Add(deleteButton);
        detail.Children.Add(new Expander
        {
            Name = "StudentDeleteExpander", Header = "Gefahrenbereich: Person endgültig löschen", IsExpanded = false, HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new Border { Background = Brush.Parse("#FFF8F8"), BorderBrush = Brush.Parse("#E09B9B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(14), Child = deletion }
        });
    }
    private void BuildImport()
    {
        Intro(importPanel, "Schüler und Vorbildung aus ASV-BW übernehmen", $"Laden Sie den ASV-BW-Export „{AsvStudentImport.FormatName}“ der Klassen 10. Das Programm erkennt dieses Format automatisch und rechnet Profil, Religion/Ethik, Fremdsprachen, Brückenkurs und bilingualen Zug um. Prüfen Sie die Vorschau: Fehlerfreie Zeilen sind vorausgewählt und werden mit einem Klick übernommen. Ein erneuter Import überschreibt keine Korrekturen und keine Wahlen.");
        importPanel.Children.Add(Help($"So erstellen Sie die Schülerliste in ASV-BW („{AsvStudentImport.FormatName}“)",
            $"1. In ASV-BW das schuleigene Exportformat „{AsvStudentImport.FormatName}“ verwenden (unter „Exportformate verwalten“, Format „Excel“, 1. Zeile enthält Feldnamen).\n" +
            "2. Die Schülerinnen und Schüler der Klassen 10 auswählen und exportieren.\n" +
            "3. Die Excel-Datei hier unter „1 · ASV-Export auswählen“ laden. Der Abiturjahrgang wird aus dem geöffneten Jahrgang übernommen.\n\n" +
            "Spalten des Formats und ihre Übernahme:\n" +
            "• ASV-ID (Skriptfeld): dauerhafte 36-stellige ASV-ID – dieselbe ID wie im späteren Kurswahl-Export „zz Kursstufe Fächerwahl“.\n" +
            "• Familienname, Rufname, Klasse: Name und Schülerordner Klasse/Familienname.Rufname.\n" +
            "• Klassengruppe: „bili“ = durchgängig bilingual, „reg“ = nicht bilingual.\n" +
            "• Profil: „I“ = Italienisch-Profil, „IMP“ = IMP, „NWT“ = keins von beiden.\n" +
            $"• Unterrichtsfächer: REV → ER, RRK → KR, ETH → ETH; Fremdsprachen E, F, L, I, GR als Sprachlaufbahn; „{AsvStudentImport.BridgeCourseCode}“ = Informatik-Brückenkurs besucht, fehlt das Kürzel, gilt „nein“.\n\n" +
            "Leere Felder bleiben „unbekannt“. Mehrere Religions-/Ethikkürzel bei einer Person werden nicht geraten, sondern als Fehler in der Vorschau gezeigt. Sportbefreiungen und Freigaben führt dieses Format nicht; sie bleiben unbekannt und lassen sich in den Schülerdetails nachtragen.\n\n" +
            "Andere Tabellen: Eine eigene Excel- oder CSV-Liste (Semikolon, UTF-8, genau ein Tabellenblatt, keine Formeln) kann über die Spaltenzuordnung unter 2 übernommen werden.\n" +
            "Erprobungsbetrieb: Im aktuellen Stand sind nur synthetische demo-IDs zugelassen (z. B. samples/klasse10-demo.csv); echte ASV-IDs werden erst mit der Produktivfreigabe angenommen."));
        var mappingStack = new StackPanel { Spacing = 8 };
        mappingStack.Children.Add(Text("2 · Spalten zuordnen (nur nötig, wenn Überschriften nicht erkannt wurden): Wählen Sie rechts die passende Spalte Ihrer Tabelle. * = Pflichtfeld. Nicht zugeordnete Voraussetzungen bleiben „Unbekannt“; ein fehlender Abgängerstatus gilt als aktiv."));
        mappingStack.Children.Add(Help("Was bedeuten die Angaben?", "Schüler-ID: eindeutige Kennung einer Person. Windows-Benutzername: ihr Schulkonto (optional). Persönlicher Wahlordner: enthält später Profil und Kurswahl; ohne Angabe wird Klasse/Nachname.Vorname verwendet.\n\nVoraussetzungen wie IMP oder bilingual: ja/nein; leer bedeutet unbekannt. Sportbefreiung: z. B. 1,2 für die betroffenen Halbjahre, „keine“ für bestätigte Nichtbefreiung. Sprachen: Fachkürzel wie E,F. Religion/Ethik: ETH, ER oder KR.\n\nWeitere Erläuterungen erscheinen, wenn Sie mit der Maus über ein Feld fahren. Unbekannte Angaben sind weder erfüllt noch falsch."));
        mappingStack.Children.Add(mapping);
        mappingStack.Children.Add(Button("3 · Vorschau aktualisieren", () =>
        {
            Ready(); if (table is null || importOrigin is null) throw new InvalidOperationException("Bitte zuerst unter 1 den ASV-Export bzw. eine Schülerliste auswählen.");
            var map = mapping.Read();
            var mappingHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", map.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)))));
            importRows = StudentTableImport.Preview(table, map, importOrigin with { MappingVersion = importOrigin.MappingVersion.Split(':')[0] + ":" + mappingHash }, Actor); importResults.Children.Clear();
            ShowImportPreview();
            SetStatus("Importvorschau aktualisiert.", FeedbackKind.Info);
        }, "Nach einer geänderten Spaltenzuordnung die Vorschau neu berechnen. Fehlerfreie Zeilen sind für die gemeinsame Übernahme vorausgewählt."));
        importMappingExpander = new Expander
        {
            Name = "ImportMappingExpander",
            Header = "2 · Spaltenzuordnung prüfen / anpassen (bei passenden Überschriften automatisch)",
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = mappingStack
        };
        importPanel.Children.Add(Primary(Button($"1 · ASV-Export „{AsvStudentImport.FormatName}“ auswählen (.xlsx / .csv) …", async () =>
        {
            Ready(); var path = await Pick($"ASV-Export „{AsvStudentImport.FormatName}“ (Klassen 10) auswählen", "ASV-Export bzw. Schülerliste (Excel oder CSV)", "*.xlsx", "*.csv"); if (path is null) return;
            if (Path.GetExtension(path).ToLowerInvariant() is not (".csv" or ".xlsx")) throw new ArgumentException("Bitte eine Schülerliste als .xlsx oder .csv auswählen. Kurswahlen werden unter „Kurswahlen einsammeln“ gelesen.");
            if (new FileInfo(path).Length > 16_000_000) throw new InvalidDataException("Importdatei überschreitet 16 MB.");
            var bytes = await File.ReadAllBytesAsync(path);
            table = Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ? StudentTableImport.Xlsx(new MemoryStream(bytes)) : StudentTableImport.Csv(Encoding.UTF8.GetString(bytes));
            var mappingVersion = "m3-table-v1"; importFormatNotice = null;
            if (AsvStudentImport.Detect(table))
            {
                var year = session?.Catalog.GraduationYear ?? throw new InvalidOperationException("Bitte zuerst einen Jahrgang öffnen.");
                var rawRows = table.Rows.Length;
                table = AsvStudentImport.Convert(table, year); mappingVersion = AsvStudentImport.MappingVersion;
                importFormatNotice = $"ASV-Format „{AsvStudentImport.FormatName}“ erkannt: {rawRows} Zeilen, Abiturjahrgang {year}. Profil, Religion/Ethik, Fremdsprachen, Brückenkurs und bilingualer Zug wurden aus den ASV-Kürzeln umgerechnet.";
            }
            importOrigin = new(Path.GetFileName(path), Convert.ToHexString(SHA256.HashData(bytes)), mappingVersion, DateTimeOffset.UtcNow, Actor);
            mapping.Load(table);
            importRows = []; importResults.Children.Clear();
            try
            {
                var map = mapping.Read();
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", map.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)))));
                importRows = StudentTableImport.Preview(table, map, importOrigin with { MappingVersion = importOrigin.MappingVersion.Split(':')[0] + ":" + hash }, Actor);
                importMappingExpander.IsExpanded = false;
                ShowImportPreview();
                SetStatus("Liste geladen. Vorschau prüfen; Spaltenzuordnung nur bei Bedarf anpassen.", FeedbackKind.Info);
            }
            catch (ArgumentException ex)
            {
                importMappingExpander.IsExpanded = true;
                importResults.Children.Add(new Border
                {
                    Background = Brush.Parse("#FAEEDA"), BorderBrush = Brush.Parse("#C48723"), BorderThickness = new Thickness(4, 1, 1, 1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 8),
                    Child = Text($"Spalten konnten nicht alle automatisch zugeordnet werden: {ex.Message}\nBitte ordnen Sie oben die fehlenden Pflichtspalten zu und klicken Sie auf „3 · Vorschau aktualisieren“.")
                });
                SetStatus("Bitte Spalten zuordnen (Bereich „2 · Spaltenzuordnung prüfen / anpassen“): " + ex.Message, FeedbackKind.Warning);
            }
        }, $"Öffnet den ASV-Export „{AsvStudentImport.FormatName}“ oder eine andere Schülerliste (Excel/CSV). Das Laden allein verändert noch keine Stammdaten.")));
        importPanel.Children.Add(importMappingExpander);
        importPanel.Children.Add(importResults);
    }
    private void BuildProfiles()
    {
        BuildSimpleDistribution();
        var advanced = new StackPanel { Spacing = 8 };
        Intro(advanced, "Einzelfälle: Profile gezielt erstellen oder erneut zustellen", "Im Normalfall genügt die gemeinsame Verteilung oben – sie erstellt fehlende oder veraltete Profile automatisch und legt Profil und Fachangebot in alle Wahlordner. Diesen Bereich brauchen Sie nur, um einzelne Personen gezielt zu bearbeiten oder den Verteilungsbericht einzusehen.");
        advanced.Children.Add(Help("Wann brauche ich was?", "Erste Verteilung, neue Schüler, geänderte Vorbildung oder Kursstreichung: oben „Ziele prüfen und gemeinsame Verteilung vorbereiten“.\n\nNur einzelne Personen nachträglich bedienen: hier auswählen, „Neue Profile erstellen“ (nur bei geänderter Vorbildung nötig) und danach „An Wahlordner zustellen“.\n\nNach einem Zustellfehler (z. B. Ordner gesperrt): nur „An Wahlordner zustellen“ wiederholen.\n\nDie Kurswahl der Schüler (aktuell.json) wird nie verändert."));
        advanced.Children.Add(Text("Personen auswählen · Strg- oder Umschalttaste für mehrere"));
        advanced.Children.Add(profileTargets);
        var advancedActions = new WrapPanel { Orientation = Orientation.Horizontal };
        var issue = Button("Neue Profile erstellen", () =>
        {
            Ready(); var distribution = new ProfileDistribution(store!); var count = 0; var failed = new List<string>();
            foreach (var target in profileTargets.SelectedItems!.Cast<Target>())
            {
                try { distribution.Issue(target.Record); count++; }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    failed.Add(target.Record.Student.DisplayName + ": " + ex.Message);
                    store!.LogDistribution(new(Guid.NewGuid(), target.Record.Student.ExternalStudentId, 0, DateTimeOffset.UtcNow, "Profil_abweichend", "Profilerstellung fehlgeschlagen; Ordnerzuordnung prüfen."));
                }
            }
            SetStatus($"{count} Profile neu erstellt, aber noch nicht zugestellt. Jetzt „An Wahlordner zustellen“ klicken." + (failed.Count > 0 ? " " + string.Join(" · ", failed) : ""),
                failed.Count > 0 ? FeedbackKind.Warning : FeedbackKind.Info);
        }, "Erstellt für die ausgewählten Personen eine neue Profilversion aus der aktuellen Vorbildung. Die Dateien liegen danach noch nicht im Schülerordner.");
        issue.Margin = new Thickness(0, 0, 10, 8); advancedActions.Children.Add(issue);
        var deliver = Button("An Wahlordner zustellen", async () =>
        {
            Ready(); var distribution = new ProfileDistribution(store!); var reports = new List<string>();
            var targets = profileTargets.SelectedItems!.Cast<Target>().Select(t => t.Record).ToArray();
            var catalog = DistributedCatalog(); var programDirectory = EffectiveProgramDirectory();
            await RunWithProgress(targets.Length, async report =>
            {
                StudentProgramSource? program = null;
                if (programDirectory.Length > 0)
                {
                    report(0, "Schülerprogramm wird eingelesen …");
                    try { program = await Task.Run(() => StudentProgramSource.Load(programDirectory)); }
                    catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { reports.Add("Schülerprogramm nicht mitverteilt: " + ex.Message); }
                }
                for (var i = 0; i < targets.Length; i++)
                {
                    var record = targets[i];
                    report(i, $"{i + 1} von {targets.Length}: {record.Student.DisplayName}");
                    reports.Add(await Task.Run(() =>
                    {
                        var issued = store!.LatestProfile(record.Student.ExternalStudentId);
                        if (issued is null) return record.Student.DisplayName + ": noch kein Profil erstellt";
                        var entry = distribution.Distribute(record, issued, catalog);
                        var programText = "";
                        if (program is not null)
                        {
                            var delivery = distribution.DeliverProgram(store.Students().Single(s => s.Student.ExternalStudentId == record.Student.ExternalStudentId), program);
                            programText = delivery.Status == "verteilt" ? " · Programm bereitgestellt" : " · Programm: " + DistributionText(delivery.Status);
                        }
                        return record.Student.DisplayName + ": " + DistributionText(entry.Status) + programText;
                    }));
                }
            });
            SetStatus(string.Join(" · ", reports), FeedbackKind.Info);
        }, "Kopiert das zuletzt erstellte Profil und das aktuelle Fachangebot in die bestätigten Wahlordner. Vorhandene Kurswahlen werden nicht verändert.");
        deliver.Margin = new Thickness(0, 0, 10, 8); advancedActions.Children.Add(deliver);
        advancedActions.Children.Add(Button("Fachangebot als Datei speichern …", ExportCurrentCatalog, "Nur für Sonderfälle: speichert das aktuelle Fachangebot separat, z. B. für eine zentrale Schülerinstallation. Beim normalen Verteilen wird es automatisch mitkopiert."));
        advanced.Children.Add(advancedActions);
        var report = new TextBox { IsReadOnly = true, AcceptsReturn = true, MinHeight = 180, TextWrapping = TextWrapping.Wrap };
        advanced.Children.Add(Button("Verteilungsbericht anzeigen", () =>
        {
            Ready(); var names = store!.Students().ToDictionary(s => s.Student.ExternalStudentId, s => s.Student.DisplayName);
            report.Text = string.Join(Environment.NewLine, store.DistributionLog().Reverse().Select(e => $"{e.At.LocalDateTime:g} · {names.GetValueOrDefault(e.StudentId, e.StudentId)} · Profilversion {e.ProfileRevision} · {DistributionText(e.Status)} · {e.Detail}"));
        }, "Zeigt alle Zustellversuche, die neuesten zuerst.")); advanced.Children.Add(report);
        profilePanel.Children.Add(new Expander { Header = "Einzelfälle: Profile einzeln erstellen/zustellen und Verteilungsbericht", Content = advanced, HorizontalAlignment = HorizontalAlignment.Stretch });
    }
    internal static string DistributionText(string status) => status switch
    {
        "verteilt" => "zugestellt",
        "nicht_gefunden" => "Ordner nicht gefunden",
        "Zugriff_verweigert" => "kein Schreibzugriff",
        "Profil_abweichend" => "vorhandene Datei weicht ab (nicht überschrieben)",
        "veraltet" => "Profil veraltet – neu erstellen",
        "Programm_abweichend" => "vorhandene Programmdateien weichen ab (nicht überschrieben)",
        _ => status
    };
    private void BuildCollection()
    {
        Intro(collectionPanel, "Ausgefüllte Kurswahlen in den Lehrerbestand übernehmen", "Das Schülerprogramm speichert die Wahl als aktuell.json im persönlichen Wahlordner. Bei geöffnetem Lehrerprogramm werden neue Wahlen alle 60 Sekunden automatisch eingelesen und gegen die aktuellen Stammdaten geprüft. Sie müssen hier nur eingreifen, wenn ein Konflikt angezeigt wird.");

        var liveStack = new StackPanel { Spacing = 8 };
        liveStack.Children.Add(Heading("Laufender Wahleingang"));
        liveStack.Children.Add(Primary(Button("Jetzt auf neue Kurswahlen prüfen", CollectAutomatically, "Sofort einlesen, ohne auf die automatische Prüfung zu warten.")));
        liveStack.Children.Add(collectionResults);
        liveStack.Children.Add(Help("Was ist ein Konflikt?", "Ein Konflikt entsteht, wenn die Beratung die Wahl einer Person bereits geändert hat und danach eine andere Schülerdatei im Wahlordner auftaucht. Das Programm übernimmt die Schülerdatei dann nicht automatisch.\n\n• „Übernehmen“: Die Schülerwahl wird neuer aktueller Stand (Begründung nötig, Häkchen oben setzen).\n• „Verwerfen“: Der Stand der Beratung bleibt; dieselbe Schülerdatei wird nicht erneut gemeldet.\n\nIn beiden Fällen bleibt die Historie vollständig erhalten."));
        liveStack.Children.Add(Help("Welche Datei muss ich auswählen?", "Keine. Für jede Person muss lediglich der persönliche Wahlordner hinterlegt und bestätigt sein – das geschieht bei der gemeinsamen Verteilung automatisch. Gesucht wird darin " + StudentFolderLayout.RootFolder + "\\" + StudentFolderLayout.DataFolder + "\\aktuell.json.\n\nprofil.json enthält Voraussetzungen, keine gewählten Kurse. Excel-/CSV-Schülerlisten gehören in den Schülerlisten-Import. Ein PDF-Kontrollbogen lässt sich nicht als Kurswahl einlesen."));
        collectionPanel.Children.Add(Surface(liveStack, 14));

        var conflictStack = new StackPanel { Spacing = 8 };
        conflictStack.Children.Add(Heading("Offene Wahlkonflikte entscheiden"));
        var conflictErrorBox = InlineErrorBox("CollectionConflictError", out var conflictErrorText);
        conflict.IsCheckedChanged += (_, _) => SetInlineError(conflictErrorBox, conflictErrorText, null);
        reason.TextChanged += (_, _) => SetInlineError(conflictErrorBox, conflictErrorText, null);
        conflictStack.Children.Add(conflict); conflictStack.Children.Add(reason); conflictStack.Children.Add(conflictErrorBox);
        var conflictActions = new WrapPanel { Orientation = Orientation.Horizontal };
        var acceptConflicts = Button("Ausgewählte Konflikte übernehmen (Schülerwahl gilt)", () =>
        {
            Ready(); var results = new List<string>(); var errors = new List<string>(); var remaining = ImmutableArray.CreateBuilder<CollectionPreview>();
            var chosen = conflictSelection.Count > 0 ? conflictSelection.Select(c => (c.Box.IsChecked == true, c.Preview)).ToArray() : collections.Select(p => (true, p)).ToArray();
            foreach (var (selected, preview) in chosen)
            {
                if (!selected) { remaining.Add(preview); continue; }
                try { session!.Confirm(preview, true, conflict.IsChecked == true, reason.Text ?? ""); results.Add(preview.Student.Student.DisplayName + ": übernommen"); }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { remaining.Add(preview); results.Add(preview.Student.Student.DisplayName + ": " + ex.Message); errors.Add(ex.Message); }
            }
            collections = remaining.ToImmutable(); conflict.IsChecked = false;
            SetInlineError(conflictErrorBox, conflictErrorText, errors.FirstOrDefault());
            RenderCollectionStatus(0, results); Refresh();
            SetStatus(string.Join(" · ", results), errors.Count == 0 ? FeedbackKind.Success : errors.Count == chosen.Count(c => c.Item1) ? FeedbackKind.Error : FeedbackKind.Warning);
        }, "Übernimmt die ausgewählten Schülerwahlen als neuen aktuellen Stand. Häkchen oben und Begründung sind erforderlich.");
        acceptConflicts.Margin = new Thickness(0, 0, 10, 6);
        var dismissButton = Button("Ausgewählte Konflikte verwerfen (Stand der Beratung bleibt)", () =>
        {
            Ready(); var remaining = ImmutableArray.CreateBuilder<CollectionPreview>(); var dismissed = 0;
            var chosen = conflictSelection.Count > 0 ? conflictSelection.Select(c => (c.Box.IsChecked == true, c.Preview)).ToArray() : collections.Select(p => (true, p)).ToArray();
            foreach (var (selected, preview) in chosen)
            {
                if (selected) { dismissedConflicts.Add(preview.Choice.StudentId + ":" + preview.Origin.ContentHash); dismissed++; }
                else remaining.Add(preview);
            }
            collections = remaining.ToImmutable(); conflict.IsChecked = false;
            SetInlineError(conflictErrorBox, conflictErrorText, null);
            RenderCollectionStatus(0, []); Refresh(); SetStatus($"{dismissed} Konflikte verworfen; der Stand der Beratung bleibt gültig.", FeedbackKind.Success);
        }, "Behält den aktuellen Lehrerbestand bei und meldet dieselbe Schülerdatei nicht erneut.");
        conflictActions.Children.Add(acceptConflicts); conflictActions.Children.Add(dismissButton);
        conflictStack.Children.Add(conflictActions);
        conflictSection = new Border
        {
            Name = "CollectionConflictSection",
            Background = Brush.Parse("#FFF6E5"), BorderBrush = Brush.Parse("#C48723"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(14),
            IsVisible = false, Child = conflictStack
        };
        collectionPanel.Children.Add(conflictSection);

        var finalStack = new StackPanel { Spacing = 8 };
        var finalConfirm = new CheckBox { Name = "FinalCollectionConfirm", Content = "Abgabetermin ist vorbei: Wahlordner jetzt räumen" };
        var finalErrorBox = InlineErrorBox("FinalCollectionError", out var finalErrorText);
        finalConfirm.IsCheckedChanged += (_, _) => SetInlineError(finalErrorBox, finalErrorText, null);
        var finalCollection = Button("Abschließend einsammeln und Schülerordner räumen", async () =>
        {
            try
            {
                if (finalConfirm.IsChecked != true) throw new InvalidOperationException("Bitte das abschließende Einsammeln ausdrücklich bestätigen.");
                if (store is not SqliteAdministrationStore sql) throw new InvalidOperationException("Nur im lokalen Lehrerbestand möglich.");
                await CollectAutomatically();
                var results = new List<CleanupResult>();
                var records = CurrentStudents().Where(r => r.MappingConfirmed).ToArray();
                var actorName = Actor;
                await RunWithProgress(records.Length, async report =>
                {
                    for (var i = 0; i < records.Length; i++)
                    {
                        var record = records[i];
                        report(i, $"Räumen {i + 1} von {records.Length}: {record.Student.DisplayName}");
                        var accepted = dismissedConflicts.Where(k => k.StartsWith(record.Student.ExternalStudentId + ":", StringComparison.Ordinal))
                            .Select(k => k[(record.Student.ExternalStudentId.Length + 1)..]).ToHashSet();
                        results.Add(await Task.Run(() => StudentFolderCleanup.Clean(sql, record, actorName, accepted)));
                    }
                });
                finalConfirm.IsChecked = false;
                var names = CurrentStudents().ToDictionary(s => s.Student.ExternalStudentId, s => s.Student.DisplayName);
                collectionResults.Children.Clear();
                foreach (var r in results.OrderBy(r => r.Status == "geräumt"))
                    collectionResults.Children.Add(TableLine(names.GetValueOrDefault(r.StudentId, r.StudentId), r.StudentId, r.Status, r.Detail));
                if (transferBackExpander is not null) transferBackExpander.IsExpanded = true;
                SetStatus($"Abschließendes Einsammeln: {results.Count(r => r.Status == "geräumt")} von {results.Count} Wahlordnern geräumt; übrige siehe Liste. Bitte unten die Rück-Transportdatei für das Verwaltungsnetz sichern und nach sicherer Ankunft das Programm im pädagogischen Netz löschen.", FeedbackKind.Success);
            }
            catch (Exception ex) { SetInlineError(finalErrorBox, finalErrorText, ex.Message); throw; }
        }, "Liest zuerst alle neuen Wahlen ein; vorher können Sie jederzeit den Zwischenstand unter „Kurswahlen“ ansehen. Nach dem abschließenden Einsammeln liest das Programm die Ordner dieser Personen nicht mehr ein – Änderungen erfolgen dann nur noch im Lehrerprogramm. Danach werden je Person aktuell.json, Sicherung, Profil, Fachangebot und das verteilte Schülerprogramm entfernt – erst nachdem alle Dateien byte-genau im Lehrerbestand archiviert sind. Ordner mit offenem Konflikt oder nicht eingesammelter Wahl bleiben unverändert.");
        finalCollection.Name = "FinalCollection";
        var cleanupHint = new Border
        {
            Name = "PostCollectionCleanupHint",
            Background = SoftAccent, BorderBrush = Accent, BorderThickness = new Thickness(4, 1, 1, 1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 8),
            Child = Text("Nach Beendigung des Einsammelns im pädagogischen Netz:\n1. Unten über „Stand für Verwaltungsnetz speichern …“ eine .gsm-transfer-Datei erzeugen und ins Verwaltungsnetz übertragen.\n2. Transportdatei im Verwaltungsnetz öffnen und prüfen.\n3. Sobald die Transportdatei sicher im Verwaltungsnetz angekommen und geöffnet ist, dieses Lehrerprogramm im pädagogischen Netz schließen und den gesamten entpackten Paketordner (samt Programm und lokalem Arbeitsstand) sowie die ZIP-Datei vollständig löschen.")
        };
        finalStack.Children.Add(Help("Was passiert beim abschließenden Einsammeln?", "Nach dem Abgabetermin räumt das Programm die Wahlordner: Jede Datei wird vorher vollständig mit Prüfsumme im Lehrerbestand archiviert, erst dann gelöscht. Entfernt wird nur, was nachweislich eingesammelt ist; bei offenem Konflikt zuerst übernehmen oder verwerfen. Programmdateien werden nur gelöscht, wenn sie unverändert aus der eigenen Verteilung stammen. Startet eine Person das Programm danach erneut, entsteht eine neue Datei, die wie gewohnt eingesammelt wird."));
        finalStack.Children.Add(finalConfirm); finalStack.Children.Add(finalErrorBox); finalStack.Children.Add(finalCollection); finalStack.Children.Add(cleanupHint);
        collectionPanel.Children.Add(new Expander
        {
            Name = "FinalCollectionExpander",
            Header = "Nach dem Abgabetermin: abschließend einsammeln und Wahlordner räumen",
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = SubtleCard(finalStack, 14)
        });
        collectionPanel.Children.Add(NetworkTransferHint(true));
    }
    private void BuildCatalog()
    {
        ClearError();
        catalogPanel.Children.Clear(); if (session is null) return;
        Intro(catalogPanel, "Fachangebot Ihres Jahrgangs", "Diese Fächer, Kursarten und Halbjahre sieht das Schülerprogramm. Das Fachangebot wird beim Verteilen der Schülerdateien automatisch in jeden Wahlordner kopiert. Änderungen nach Eingang der ersten Wahlen nur über Kursplanung → Angebot ändern.");
        catalogPanel.Children.Add(Text($"Abitur {session.Catalog.GraduationYear} · Angebotsstand {session.Catalog.Version}"));
        catalogPanel.Children.Add(CourseAnnotationEditor());
        const string columns = "220,110,90,160,*";
        catalogPanel.Children.Add(new Border { Background = SoftAccent, Child = CompactLine(columns, [Text("Fach"), Text("Kursart"), Text("Variante"), Text("Status"), Text("Halbjahre · Wochenstunden")]) });
        var index = 0;
        foreach (var offer in session.Catalog.Offers.OrderBy(o => SubjectName(o.SubjectId), StringComparer.CurrentCulture).ThenBy(o => o.Type))
        {
            var hours = string.Join(", ", offer.Schedule.Select(h => $"HJ {h.Semester}: {h.WeeklyHours} h"));
            catalogPanel.Children.Add(new Border { Background = index++ % 2 == 0 ? RowAlt : Brushes.White,
                Child = CompactLine(columns, [Text(SubjectName(offer.SubjectId) + CourseAnnotations.Suffix(offer)), Text(FormatCourseType(offer.Type)), Text(FormatVariant(offer.Variant)), Text(FormatOfferStatus(offer.Status)), Text(hours)]) });
        }
        catalogPanel.Children.Add(Button("Fachangebot als Datei speichern …", ExportCurrentCatalog, "Nur für Sonderfälle, z. B. eine zentrale Schülerinstallation. Beim normalen Verteilen wird das Fachangebot automatisch mitkopiert."));
    }
}
