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
    private readonly ComboBox statusFilter = new() { ItemsSource = new[] { "Alle Prüfstände", "Ohne Wahl", "Regelverstoß", "Fachlich zu klären", "Formal gültig" }, SelectedIndex = 0 };
    private readonly TextBox actor = new() { Text = "demo-beratung", Watermark = "Bearbeiterkennung" };
    private string Actor => actor.Text?.Trim() is { Length: > 0 } value ? value : throw new ArgumentException("Bearbeiterkennung fehlt. Unter Einstellungen eintragen.");
    private readonly ImportColumnPicker mapping = new();
    private ImportTable? table;
    private ImportOrigin? importOrigin;
    private ImmutableArray<StudentImportRow> importRows = [];
    private readonly StackPanel importResults = new() { Spacing = 5 };
    private ImmutableArray<CollectionPreview> collections = [];
    private readonly StackPanel collectionResults = new() { Spacing = 5 };
    private readonly CheckBox conflict = new() { Content = "Angezeigte Konflikte bewusst zugunsten der eingelesenen Schülerwahl entscheiden" };
    private readonly TextBox reason = new() { Watermark = "Begründung (wird im Änderungsprotokoll gespeichert)" };
    private readonly ListBox profileTargets = new() { SelectionMode = SelectionMode.Multiple, MinHeight = 180 };
    private string? selectedStudentId;
    private sealed record Target(StudentRecord Record) { public override string ToString() => $"{Record.Student.SyntheticDisplayName} ({Record.Student.ClassName}) → {(Record.HomeDirectory.Length == 0 ? "kein Ordner" : Record.HomeDirectory)} · Ordner {(Record.MappingConfirmed ? "bestätigt" : "noch nicht bestätigt")}"; }
    private sealed record SubjectItem(string? Id, string Label) { public override string ToString() => Label; }

    private static TabItem Tab(string name, Control content) => new() { Header = name, Content = content };
    private static ScrollViewer Scroll(Control content) => new() { Content = content, Margin = new Thickness(8) };
    private Button Button(string label, Func<Task> action, string? help = null)
    {
        var button = new Button { Content = label };
        if (help is not null) ToolTip.SetTip(button, new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
        button.Click += async (_, _) => { button.IsEnabled = false; try { await action(); UpdateDashboard(); UpdateVisualNavigation(); UpdateWorkflowProgress(); } catch (Exception ex) { status.Text = "Vorgang nicht abgeschlossen: " + ex.Message; } finally { button.IsEnabled = true; } };
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
        panel.Children.Add(Text(
            "1 · Neuen Abiturjahrgang anlegen – oben neben dem Jahrgang auf „Jahrgang wechseln / neu …“ klicken.\n" +
            "2 · Leistungsfächer auswählen, die in diesem Jahrgang angeboten werden.\n" +
            "3 · Schülerliste (Excel/CSV aus Klasse 10) einlesen: Namen, Klassen und Vorbildung.\n" +
            "4 · Schülerdateien verteilen: Das Programm legt jedem Schüler sein persönliches Profil und das Fachangebot in den Wahlordner.\n" +
            "5 · Schüler wählen im Schülerprogramm. Das Lehrerprogramm sammelt die Wahlen bei geöffneter App automatisch ein.\n" +
            "6 · Wahlergebnisse prüfen, Sonderfälle im Schülerbestand klären, Abgänger markieren.\n" +
            "7 · Kursplanung: Wahlzahlen ansehen, Kurse ggf. streichen. Das Programm erstellt Nachwahlbögen; danach Schülerdateien erneut verteilen.\n" +
            "8 · Abgleich / Abschluss: Listen exportieren, ASV-Eingabe pro Person vermerken, Jahrgang sichern."));
        panel.Children.Add(Help("Was bedeuten die Fachbegriffe?",
            "LF – Leistungsfach (5-stündig, alle vier Halbjahre).\nBF – Basisfach (meist 2- oder 3-stündig).\nHJ 1–4 – die vier Halbjahre der Kursstufe; KS1 = HJ 1+2, KS2 = HJ 3+4.\n\n" +
            "Prüfstand einer Wahl:\n• Formal gültig – keine offenen Punkte.\n• Fachlich zu klären – z. B. Vorbildung unbekannt, Sportersatz, Religion/Ethik-Wechsel. Kein Fehler, aber eine Entscheidung der Oberstufenberatung ist nötig.\n• Regelverstoß – die Belegung widerspricht einer Regel (z. B. fehlendes Pflichtfach).\n\n" +
            "Vorbildung „Unbekannt“ bedeutet: Es liegt keine Angabe vor. Das Programm behandelt das weder als erfüllt noch als nicht erfüllt, sondern als zu klären.\n\n" +
            "Nachwahl – Eine Person muss nach einer Kursstreichung oder wegen eines Regelverstoßes erneut wählen.\n" +
            "Abgänger – Voraussichtliche Abgänger können in der Kursplanung ausgeblendet werden; bestätigte Abgänger zählen nie zur Planung. Ihre Wahl bleibt gespeichert.\n" +
            "Profil – kleine Datei mit den bestätigten Voraussetzungen einer Person (z. B. IMP, bilingual). Das Schülerprogramm zeigt damit die passenden Fächer an.\n" +
            "Revision – jeder gespeicherte Stand einer Wahl. Nichts wird überschrieben; Wiederherstellen legt einen neuen Stand an."));
        panel.Children.Add(Help("Verwaltungsnetz und pädagogisches Netz (Transportdatei)",
            "Schülerordner sind meist nur im pädagogischen Netz erreichbar, die Verwaltung arbeitet im Verwaltungsnetz. Vor dem Wechsel den Jahrgang als Transportdatei (.gsm-transfer) speichern und im anderen Netz über „Transportdatei öffnen …“ laden. Danach nur noch mit dem geöffneten Stand weiterarbeiten – es gibt keine automatische Zusammenführung zweier Stände."));
        panel.Children.Add(Help("Dateiübersicht: Inhalt, Herkunft und Verwendung", "Schülerliste (.xlsx / .csv)\nEine Tabelle aus Ihrer vorbereiteten Klassen-10-Liste. Unter Wahl → Schülerliste importieren. Enthält Personen und Voraussetzungen.\n\nFachangebot (.json), z. B. fachangebot-demo.json\nBeschreibt Fächer, Kursarten und Halbjahre eines Jahrgangs. Wird beim Anlegen eines Jahrgangs automatisch geladen und beim Verteilen in jeden Schülerordner kopiert.\n\nVerwaltungsdatei (.db), z. B. verwaltung.db\nSpeichert den Lehrerbestand mit Änderungen und Historie lokal. Wird automatisch angelegt und geöffnet.\n\nPersönliches Profil (profil.json)\nDas Lehrerprogramm erzeugt diese Datei aus den geprüften Voraussetzungen und legt sie beim Verteilen in den Wahlordner. Keine Schülerliste und keine Kurswahl.\n\nKurswahl (aktuell.json)\nWird vom Schülerprogramm gespeichert. Das Lehrerprogramm liest sie automatisch aus dem bestätigten Wahlordner.\n\nKontrollbogen (.pdf)\nLesbare Übersicht aus dem Schülerprogramm. Zum Ansehen und Unterschreiben; kein Importformat.\n\nTransportdatei (.gsm-transfer)\nKompletter Jahrgang für den Wechsel zwischen Verwaltungs- und pädagogischem Netz bzw. als Sicherung.\n\nASV-Dateien\nEin eigener ASV-Import folgt nach Vorliegen einer anonymisierten Musterdatei. Bis dahin: allgemeiner CSV/XLSX-Abgleich unter Abgleich / Abschluss."));
        panel.Children.Add(Help("Mit welchen Testdateien kann ich beginnen?", "Im Projektordner samples liegen:\n• fachangebot-demo.json: wird beim Anlegen eines Jahrgangs automatisch als Vorlage geladen.\n• klasse10-demo.csv: unter Schülerliste importieren laden; enthält zwei erfundene Personen.\n• schueler-demo.json: Beispiel für eine Kurswahl. Beim Einsammeln erwartet das Programm den Namen aktuell.json im bestätigten Wahlordner.\n• profil-demo-unsigned.json: Beispiel für ein persönliches Schülerprofil."));
        panel.Children.Add(Help("Nur für die Einrichtung: Programmeinstellungen", "appsettings.json im Schülerprogramm enthält die Programmeinstellungen (Speicherort, Katalogpfad). Diese Datei wird nicht als Schülerliste importiert."));
        return panel;
    }
    private static string Describe(StudentRecord r)
    {
        var p = r.Student.Prerequisites;
        return $"{r.Student.SyntheticDisplayName}, Klasse {r.Student.ClassName}, Abitur {r.Student.GraduationYear}, Abgänger: {FormatDeparture(r.Student.Departure)}\n" +
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
        foreach (var state in states)
        {
            var label = $"{state.Record.Student.SyntheticDisplayName} · {state.Record.Student.ClassName} · {state.Record.Student.ExternalStudentId}";
            if (!label.Contains(filter.Text ?? "", StringComparison.OrdinalIgnoreCase) || followupFilter.IsChecked == true && !state.RequiresReselection) continue;
            if (statusFilter.SelectedIndex == 1 && state.Active is not null || statusFilter.SelectedIndex == 2 && state.Validation?.Status != ReviewStatus.Invalid
                || statusFilter.SelectedIndex == 3 && state.Validation?.Status != ReviewStatus.NeedsReview || statusFilter.SelectedIndex == 4 && state.Validation?.Status != ReviewStatus.Valid) continue;
            var notes = string.Join(" · ", new[] { state.RequiresReselection ? "Nachwahl offen" : "", state.Record.Student.Departure == DepartureStatus.Active ? "" : "Abgänger: " + FormatDeparture(state.Record.Student.Departure), state.Record.MappingConfirmed ? "" : "Ordner nicht bestätigt" }.Where(s => s.Length > 0));
            var open = Button("Öffnen", () => ShowStudent(state), "Stammdaten, Vorbildung, Kurswahl und Historie dieser Person rechts anzeigen.");
            var selected = state.Record.Student.ExternalStudentId == selectedStudentId && personEditor.IsExpanded;
            roster.Children.Add(new Border { Background = selected ? Brush.Parse("#F0F8F6") : Brushes.Transparent, BorderBrush = selected ? Accent : Brushes.Transparent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Child = TableLine(state.Record.Student.SyntheticDisplayName, state.Record.Student.ClassName, FormatStatus(state.Validation?.Status), notes, open) });
        }
        if (!preserveEditor)
        {
            profileTargets.ItemsSource = store.Students().Where(s => s.Student.GraduationYear == session.Catalog.GraduationYear).Select(s => new Target(s)).ToArray();
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
        personEditor.Header = $"{record.Student.SyntheticDisplayName} · Klasse {record.Student.ClassName}";
        var summary = Text($"Schüler-ID {id} · Prüfstand: {FormatStatus(state.Validation?.Status)}" + (state.RequiresReselection ? " · Nachwahl offen" : "") + (state.Record.Student.Departure == DepartureStatus.Active ? "" : " · Abgänger: " + FormatDeparture(state.Record.Student.Departure)));
        summary.Foreground = Muted; detail.Children.Add(summary);

        var masterTab = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4) };
        var choiceTab = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4) };
        var historyTab = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4) };
        detail.Children.Add(Surface(masterTab, 14));
        detail.Children.Add(Surface(choiceTab, 14));
        detail.Children.Add(Surface(historyTab, 14));

        // ---- Tab 1: account, folder, prerequisites
        masterTab.Children.Add(Heading("Konto und Wahlordner"));
        var user = new TextBox { Name = "StudentUsername", Text = record.WindowsUsername, Watermark = "DOMÄNE\\Benutzername (optional)" };
        var home = new TextBox { Name = "StudentHomeDirectory", Text = record.HomeDirectory, Watermark = "Persönlicher Wahlordner" };
        var confirmed = new CheckBox { Name = "StudentMappingConfirmed", Content = "Ich habe geprüft, dass dieser Ordner wirklich zu dieser Person gehört", IsChecked = record.MappingConfirmed };
        masterTab.Children.Add(Labeled("Windows-Konto", user, "Optionales schulisches Windows-Konto der Person in der Form DOMÄNE\\Benutzername."));
        masterTab.Children.Add(Labeled("Wahlordner", home, "Ordner, in dem das Schülerprogramm die Kurswahl (aktuell.json) speichert und in den das Lehrerprogramm Profil und Fachangebot legt. Vor der ersten Verteilung genügt der relative Ordner Klasse/Nachname.Vorname; bei der Verteilung wird er zum vollständigen Pfad."));
        ToolTip.SetTip(confirmed, "Nur bestätigte Ordner werden beim Einsammeln gelesen. Bei der gemeinsamen Verteilung wird die Bestätigung automatisch gesetzt.");
        masterTab.Children.Add(confirmed);
        masterTab.Children.Add(Button("Zuordnung speichern", () =>
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
            Refresh(); status.Text = "Zuordnung gespeichert.";
        }, "Speichert Konto und Wahlordner als neuen Stand. Frühere Stände bleiben in der Historie."));

        var p = record.Student.Prerequisites;
        masterTab.Children.Add(Heading("Vorbildung und Abgängerstatus"));
        masterTab.Children.Add(new TextBlock { Text = "„Unbekannt“ heißt: keine Angabe. Das Programm wertet das als „fachlich zu klären“, nicht als erfüllt oder nicht erfüllt. Fahren Sie mit der Maus über ⓘ für Erklärungen.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
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

        masterTab.Children.Add(Labeled("Abgängerstatus", departureBox, "Aktiv: zählt normal. Voraussichtlich: kann in der Kursplanung ausgeblendet werden. Bestätigt: zählt nie zur Planung. Die Wahl bleibt in jedem Fall gespeichert."));
        masterTab.Children.Add(Labeled("Italienisch-Profil (3. Fremdsprache)", italianBox, "Italienisch als dritte Fremdsprache im Profil der Klasse 10. Schaltet BF Italienisch frei. Ob Italienisch als fortgeführte Fremdsprache zählt, wird unten bei den Sprachen gesondert bestätigt."));
        masterTab.Children.Add(Labeled("Informatik-Brückenkurs Klasse 10", bridgeBox, "Brückenkurs oder IMP schaltet BF Informatik frei. Beide Wege werden getrennt erfasst."));
        masterTab.Children.Add(Labeled("IMP-Profil besucht", impBox, "Profil Informatik-Mathematik-Physik. Brückenkurs oder IMP schaltet BF Informatik frei."));
        masterTab.Children.Add(Labeled("Durchgängig bilingual", biliBox, "Durchgängiger bilingualer Bildungsgang. Voraussetzung für Biologie bilingual (LF und BF)."));
        masterTab.Children.Add(Labeled("Religion/Ethik in Klasse 10", relGrade10Box, "In Klasse 10 besuchtes Fach. Ein Wechsel in der Kursstufe sperrt die Basisfachwahl nicht, schränkt aber die mündliche Prüfung ein."));
        masterTab.Children.Add(Labeled("Religion/Ethik: Belegung freigegeben", relPermBox, "Liegt eine ggf. nötige Zustimmung zur Belegung vor (z. B. Religionsunterricht einer anderen Konfession)?"));
        masterTab.Children.Add(Labeled("Religion/Ethik: Prüfung freigegeben", relExamPermBox, "Ist die mündliche Prüfung im gewählten Fach trotz Wechsel zugelassen, z. B. nach bestandener Feststellungsprüfung?"));
        masterTab.Children.Add(Labeled("Sportbefreiung", sportBox, "Halbjahre mit Sportbefreiung. Für jedes befreite Halbjahr ist ein zusätzlicher Basis- oder Wahlfachkurs nötig; der Seminarkurs zählt nicht. Keine Diagnosen eintragen."));
        masterTab.Children.Add(Labeled("Dauerhafte Sportbefreiung", permSportBox, "Dauerhafte, auch teilweise Befreiung zum Wahlzeitpunkt. Schließt LF Sport als normale Wahl aus."));
        masterTab.Children.Add(Labeled("Fortgeführte Fremdsprachen", langBox, "Sprachen mit bestätigter Sprachlaufbahn (z. B. E, F, L). Englisch, Französisch und Latein zählen ohnehin; wichtig ist die Bestätigung vor allem für Italienisch und weitere Sprachen."));
        var savePrereq = Button("Vorbildung und Abgängerstatus speichern", () =>
        {
            var sportText = (sportBox.Text ?? "").Trim().ToLowerInvariant();
            ImmutableArray<int>? semesters = sportText is "" or "unknown" or "unbekannt" ? null
                : sportText is "keine" or "none" ? []
                : [.. sportText.Split(',', StringSplitOptions.TrimEntries).Select(int.Parse).Order()];
            var updatedPrior = new PriorEducation((Knowledge)italianBox.SelectedItem!, (Knowledge)bridgeBox.SelectedItem!, (Knowledge)impBox.SelectedItem!, (Knowledge)biliBox.SelectedItem!, ((SubjectItem)relGrade10Box.SelectedItem!).Id, semesters)
            {
                ContinuedLanguages = [.. (langBox.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct()],
                ReligionEthicsPermission = (Knowledge)relPermBox.SelectedItem!,
                ReligionEthicsExamPermission = (Knowledge)relExamPermBox.SelectedItem!,
                PermanentSportExemption = (Knowledge)permSportBox.SelectedItem!
            };
            store!.SaveStudent(record with
            {
                Student = record.Student with { Departure = (DepartureStatus)departureBox.SelectedItem!, Prerequisites = updatedPrior },
                Version = record.Version + 1, Actor = Actor, ChangedAt = DateTimeOffset.UtcNow
            }, record.Version);
            Refresh(); status.Text = "Vorbildung und Abgängerstatus gespeichert. Bei geänderter Vorbildung danach Schülerdateien erneut verteilen.";
        }, "Speichert als neuen Stand. Geänderte Vorbildung erfordert eine neue Verteilung der Schülerdateien, damit das Schülerprogramm sie kennt.");
        savePrereq.Name = "SavePrerequisites"; masterTab.Children.Add(Primary(savePrereq));

        // ---- Tab 2: findings, follow-up, teacher edit
        if (state.Validation is { } validation && validation.Messages.Length > 0)
        {
            choiceTab.Children.Add(Heading("Prüfhinweise"));
            foreach (var message in validation.Messages.OrderBy(m => m.Rule.Severity == ReviewStatus.Invalid ? 0 : 1))
            {
                var invalid = message.Rule.Severity == ReviewStatus.Invalid;
                var line = Text((invalid ? "✗ Regelverstoß: " : "• Zu klären: ") + message.Explanation);
                line.Foreground = invalid ? Brush.Parse("#8C1D1D") : Ink;
                ToolTip.SetTip(line, $"{message.Rule.Description}\nAbhilfe: {message.Remedy}\nRegel {message.Rule.Id} · Quelle: {message.Rule.Source}");
                choiceTab.Children.Add(line);
            }
        }
        else if (state.Active is null) choiceTab.Children.Add(Text("Für diese Person wurde noch keine Kurswahl eingelesen. Sie können unten eine Wahl im Namen der Beratung erfassen."));
        choiceTab.Children.Add(Heading("Nachwahl"));
        if (!string.IsNullOrWhiteSpace(state.Note)) choiceTab.Children.Add(Text("Vermerk: " + state.Note));
        var editReason = new TextBox { Name = "StudentEditReason", Watermark = "Begründung für Nachwahl, Änderung oder Wiederherstellung (Pflicht für das Änderungsprotokoll)" };
        var deadline = new CalendarDatePicker { Name = "ReselectionDeadline", SelectedDate = state.ReselectionDeadline?.LocalDateTime, Watermark = "Nachwahlfrist (optional)" };
        var followRow = new WrapPanel { Orientation = Orientation.Horizontal };
        deadline.Margin = new Thickness(0, 0, 10, 0); followRow.Children.Add(deadline);
        var mark = Button("Nachwahl vormerken", () =>
        {
            session!.FollowUp(id, true, deadline.SelectedDate is { } d ? new DateTimeOffset(d.Date) : null, editReason.Text ?? "");
            Refresh(); status.Text = "Nachwahl vorgemerkt und protokolliert.";
        }, "Markiert die Person als „Nachwahl offen“. Erscheint im Filter, auf der Übersicht und in den Wahlergebnissen.");
        mark.Margin = new Thickness(0, 0, 10, 0); followRow.Children.Add(mark);
        followRow.Children.Add(Button("Nachwahl abschließen", () => { session!.FollowUp(id, false, null, editReason.Text ?? ""); Refresh(); status.Text = "Nachwahl abgeschlossen."; }, "Beendet die offene Nachwahl; der Vorgang bleibt im Protokoll."));
        choiceTab.Children.Add(followRow);

        var active = state.Active;
        var choice = active?.Snapshot ?? new CourseChoice(2, Guid.NewGuid(), id, record.Student.GraduationYear, session!.Catalog.Version, GsmKurswahl.Rules.ChoiceValidator.Version, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, SubmissionStatus.Draft, ElectionPhase.Preliminary, [], []);
        choiceTab.Children.Add(Heading("Kurswahl durch die Beratung ändern"));
        choiceTab.Children.Add(new TextBlock { Text = "Haken je Halbjahr setzen oder entfernen. Die ursprüngliche Schülerwahl bleibt in der Historie erhalten.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var selections = new List<(CourseOffer Offer, int Semester, CheckBox Box)>();
        foreach (var offer in session!.Catalog.Offers)
        {
            var isOffered = offer.Status is OfferStatus.Offered or OfferStatus.Established;
            var alreadyChosen = choice.Selections.Any(s => s.OfferId == offer.Id);
            if (!isOffered && !alreadyChosen) continue;
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            line.Children.Add(new TextBlock { Text = OfferLabel(offer.Id) + (isOffered ? "" : $" ({FormatOfferStatus(offer.Status)})"), Width = 240, VerticalAlignment = VerticalAlignment.Center, Foreground = isOffered ? Ink : Brush.Parse("#8C1D1D") });
            foreach (var semester in offer.Schedule)
            {
                var box = new CheckBox { Content = "HJ " + semester.Semester, IsChecked = choice.Selections.Any(s => s.OfferId == offer.Id && s.Semesters.Contains(semester.Semester)) };
                line.Children.Add(box); selections.Add((offer, semester.Semester, box));
            }
            choiceTab.Children.Add(line);
        }
        var oralItems = new[] { new SubjectItem(null, "— nicht geplant —") }.Concat(session.Catalog.Offers.Where(o => o.Type == CourseType.Bf).Select(o => o.SubjectId).Distinct()
            .Select(s => new SubjectItem(s, SubjectName(s)))).ToArray();
        ComboBox OralBox(string name, string? current) => new() { Name = name, ItemsSource = oralItems, SelectedItem = oralItems.FirstOrDefault(i => i.Id == current) ?? oralItems[0], Width = 220, Margin = new Thickness(0, 0, 10, 0) };
        var oral1 = OralBox("OralSubject1", choice.PlannedOralSubjects.ElementAtOrDefault(0));
        var oral2 = OralBox("OralSubject2", choice.PlannedOralSubjects.ElementAtOrDefault(1));
        var oralRow = new WrapPanel { Orientation = Orientation.Horizontal }; oralRow.Children.Add(oral1); oralRow.Children.Add(oral2);
        choiceTab.Children.Add(Labeled("Geplante mündliche Prüfungsfächer", oralRow, "Unverbindliche Planung der zwei mündlichen Prüfungsfächer. Zusammen mit den drei Leistungsfächern müssen Deutsch, Mathematik und alle drei Aufgabenfelder abgedeckt sein."));
        var saveChoice = Button("Änderung als neuen Stand speichern", () =>
        {
            var selected = selections.Where(s => s.Box.IsChecked == true).GroupBy(s => s.Offer).Select(g => new CourseSelection(g.Key.Id, g.Key.SubjectId, g.Key.Type, g.Key.Variant, [.. g.Select(v => v.Semester)])).ToImmutableArray();
            var oral = new[] { ((SubjectItem)oral1.SelectedItem!).Id, ((SubjectItem)oral2.SelectedItem!).Id }.OfType<string>().Distinct().ToImmutableArray();
            session.Save(choice with { OfferVersion = session.Catalog.Version, RuleVersion = GsmKurswahl.Rules.ChoiceValidator.Version, Revision = active is null ? 1 : active.Snapshot.Revision + 1, Selections = selected, PlannedOralSubjects = oral, UpdatedAt = DateTimeOffset.UtcNow }, active?.Id, RevisionSource.TeacherEdit, editReason.Text ?? "");
            Refresh(); status.Text = "Lehreränderung gespeichert und gegen die aktuellen Stammdaten geprüft.";
        }, "Speichert die geänderte Belegung als neuen Stand mit Begründung. Die Begründung oben ist Pflicht.");
        saveChoice.Name = "SaveTeacherChoice"; choiceTab.Children.Add(Primary(saveChoice));

        // ---- Tab 3: history
        historyTab.Children.Add(new TextBlock { Text = "Jeder Stand ist vollständig gespeichert. „Wiederherstellen“ legt einen neuen Stand an; nichts wird gelöscht. Die Begründung dafür im Reiter „Kurswahl und Nachwahl“ eintragen.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var history = store!.History(id);
        if (history.IsEmpty) historyTab.Children.Add(Text("Noch keine gespeicherten Stände."));
        foreach (var revision in history.Reverse())
        {
            var diff = string.Join("\n", revision.Changes.Select(c => $"   {OfferLabel(c.OfferId)}: {Semesters(c.Before)} → {Semesters(c.After)}"));
            var oralSummary = revision.Snapshot.PlannedOralSubjects.IsEmpty ? "keine" : string.Join(", ", revision.Snapshot.PlannedOralSubjects.Select(SubjectName));
            var card = new StackPanel { Spacing = 4 };
            card.Children.Add(new TextBlock { Text = $"{revision.ChangedAt.LocalDateTime:g} · {FormatRevisionSource(revision.Source)} · {revision.ActorId} ({FormatActorRole(revision.ActorRole)})", FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            card.Children.Add(Text($"Begründung: {revision.Reason}\nMündlich geplant: {oralSummary}" + (diff.Length > 0 ? "\nÄnderungen:\n" + diff : "")));
            if (active is not null && revision.Id != active.Id)
                card.Children.Add(Button("Diesen Stand wiederherstellen", () => { session.Restore(id, revision.Id, active.Id, editReason.Text ?? ""); Refresh(); status.Text = "Früherer Stand als neuer Stand wiederhergestellt."; },
                    "Übernimmt diesen früheren Stand als neue, aktuelle Wahl. Begründung im Reiter „Kurswahl und Nachwahl“ eintragen."));
            historyTab.Children.Add(Surface(card, 10));
        }
    }
    private void BuildImport()
    {
        Intro(importPanel, "Schüler und Vorbildung aus einer Tabelle übernehmen", "Laden Sie die Klassen-10-Liste mit Namen, Klassen und persönlichen Voraussetzungen. Passende Spaltenüberschriften werden automatisch zugeordnet. Prüfen Sie die Vorschau: Fehlerfreie Zeilen sind vorausgewählt und werden mit einem Klick übernommen. Ein erneuter Import überschreibt keine Korrekturen und keine Wahlen.");
        importPanel.Children.Add(Help("Welche Tabelle kann ich verwenden?", "Excel (.xlsx): genau ein Tabellenblatt mit einer Kopfzeile und festen Werten; Formeln vorher als Werte speichern.\nCSV (.csv): Texttabelle mit Kopfzeile, Semikolon als Trennzeichen und UTF-8-Zeichencodierung.\n\nZum Ausprobieren: samples/klasse10-demo.csv. Profil-, Angebots- und Kurswahldateien (.json) gehören nicht in diesen Import. Ein spezieller ASV-Adapter ist noch nicht verfügbar."));
        importPanel.Children.Add(Primary(Button("1 · Schülerliste auswählen (.xlsx / .csv) …", async () =>
        {
            Ready(); var path = await Pick("Schülerliste mit Personen und Vorbildung auswählen", "Schülerlisten (Excel oder CSV)", "*.xlsx", "*.csv"); if (path is null) return;
            if (Path.GetExtension(path).ToLowerInvariant() is not (".csv" or ".xlsx")) throw new ArgumentException("Bitte eine Schülerliste als .xlsx oder .csv auswählen. Kurswahlen werden unter „Kurswahlen einsammeln“ gelesen.");
            if (new FileInfo(path).Length > 16_000_000) throw new InvalidDataException("Importdatei überschreitet 16 MB.");
            var bytes = await File.ReadAllBytesAsync(path);
            table = Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ? StudentTableImport.Xlsx(new MemoryStream(bytes)) : StudentTableImport.Csv(Encoding.UTF8.GetString(bytes));
            importOrigin = new(Path.GetFileName(path), Convert.ToHexString(SHA256.HashData(bytes)), "m3-table-v1", DateTimeOffset.UtcNow, Actor);
            mapping.Load(table);
            importRows = []; importResults.Children.Clear(); status.Text = "Liste geladen. Vorschau prüfen; Spaltenzuordnung nur bei Bedarf anpassen.";
            try
            {
                var map = mapping.Read();
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", map.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)))));
                importRows = StudentTableImport.Preview(table, map, importOrigin with { MappingVersion = "m3-table-v1:" + hash }, Actor);
                ShowImportPreview();
            }
            catch (ArgumentException ex) { status.Text = "Bitte Spalten zuordnen (Bereich „Spaltenzuordnung anpassen“): " + ex.Message; }
        }, "Öffnet eine Excel- oder CSV-Tabelle. Das Laden allein verändert noch keine Stammdaten.")));
        importPanel.Children.Add(Text("2 · Spalten zuordnen (nur nötig, wenn Überschriften nicht erkannt wurden): Wählen Sie rechts die passende Spalte Ihrer Tabelle. * = Pflichtfeld. Nicht zugeordnete Voraussetzungen bleiben „Unbekannt“; ein fehlender Abgängerstatus gilt als aktiv."));
        importPanel.Children.Add(Help("Was bedeuten die Angaben?", "Schüler-ID: eindeutige Kennung einer Person. Windows-Benutzername: ihr Schulkonto (optional). Persönlicher Wahlordner: enthält später Profil und Kurswahl; ohne Angabe wird Klasse/Nachname.Vorname verwendet.\n\nVoraussetzungen wie IMP oder bilingual: ja/nein; leer bedeutet unbekannt. Sportbefreiung: z. B. 1,2 für die betroffenen Halbjahre, „keine“ für bestätigte Nichtbefreiung. Sprachen: Fachkürzel wie E,F. Religion/Ethik: ETH, ER oder KR.\n\nWeitere Erläuterungen erscheinen, wenn Sie mit der Maus über ein Feld fahren. Unbekannte Angaben sind weder erfüllt noch falsch."));
        importPanel.Children.Add(new Expander { Header = "Spaltenzuordnung anpassen (bei passenden Überschriften automatisch)", Content = mapping });
        importPanel.Children.Add(Button("3 · Vorschau aktualisieren", () =>
        {
            Ready(); if (table is null || importOrigin is null) throw new InvalidOperationException("Bitte zuerst unter 1 eine Schülerliste auswählen.");
            var map = mapping.Read();
            var mappingHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", map.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)))));
            importRows = StudentTableImport.Preview(table, map, importOrigin with { MappingVersion = "m3-table-v1:" + mappingHash }, Actor); importResults.Children.Clear();
            ShowImportPreview();
        }, "Nach einer geänderten Spaltenzuordnung die Vorschau neu berechnen. Fehlerfreie Zeilen sind für die gemeinsame Übernahme vorausgewählt."));
        importPanel.Children.Add(importResults);
    }
    private void BuildProfiles()
    {
        BuildSimpleDistribution();
        profilePanel.Children.Insert(0, NetworkTransferHint(false));
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
                    failed.Add(target.Record.Student.SyntheticDisplayName + ": " + ex.Message);
                    store!.LogDistribution(new(Guid.NewGuid(), target.Record.Student.ExternalStudentId, 0, DateTimeOffset.UtcNow, "Profil_abweichend", "Profilerstellung fehlgeschlagen; Ordnerzuordnung prüfen."));
                }
            }
            status.Text = $"{count} Profile neu erstellt, aber noch nicht zugestellt. Jetzt „An Wahlordner zustellen“ klicken. " + string.Join(" · ", failed);
        }, "Erstellt für die ausgewählten Personen eine neue Profilversion aus der aktuellen Vorbildung. Die Dateien liegen danach noch nicht im Schülerordner.");
        issue.Margin = new Thickness(0, 0, 10, 8); advancedActions.Children.Add(issue);
        var deliver = Button("An Wahlordner zustellen", () =>
        {
            Ready(); var distribution = new ProfileDistribution(store!); var reports = new List<string>();
            foreach (var target in profileTargets.SelectedItems!.Cast<Target>())
            {
                var issued = store!.LatestProfile(target.Record.Student.ExternalStudentId);
                if (issued is null) { reports.Add(target.Record.Student.SyntheticDisplayName + ": noch kein Profil erstellt"); continue; }
                var entry = distribution.Distribute(target.Record, issued, session!.Catalog); reports.Add(target.Record.Student.SyntheticDisplayName + ": " + DistributionText(entry.Status));
            }
            status.Text = string.Join(" · ", reports);
        }, "Kopiert das zuletzt erstellte Profil und das aktuelle Fachangebot in die bestätigten Wahlordner. Vorhandene Kurswahlen werden nicht verändert.");
        deliver.Margin = new Thickness(0, 0, 10, 8); advancedActions.Children.Add(deliver);
        advancedActions.Children.Add(Button("Fachangebot als Datei speichern …", ExportCurrentCatalog, "Nur für Sonderfälle: speichert das aktuelle Fachangebot separat, z. B. für eine zentrale Schülerinstallation. Beim normalen Verteilen wird es automatisch mitkopiert."));
        advanced.Children.Add(advancedActions);
        var report = new TextBox { IsReadOnly = true, AcceptsReturn = true, MinHeight = 180, TextWrapping = TextWrapping.Wrap };
        advanced.Children.Add(Button("Verteilungsbericht anzeigen", () =>
        {
            Ready(); var names = store!.Students().ToDictionary(s => s.Student.ExternalStudentId, s => s.Student.SyntheticDisplayName);
            report.Text = string.Join(Environment.NewLine, store.DistributionLog().Reverse().Select(e => $"{e.At.LocalDateTime:g} · {names.GetValueOrDefault(e.StudentId, e.StudentId)} · Profilversion {e.ProfileRevision} · {DistributionText(e.Status)} · {e.Detail}"));
        }, "Zeigt alle Zustellversuche, die neuesten zuerst.")); advanced.Children.Add(report);
        profilePanel.Children.Add(new Expander { Header = "Einzelfälle: Profile einzeln erstellen/zustellen und Verteilungsbericht", Content = advanced });
    }
    internal static string DistributionText(string status) => status switch
    {
        "verteilt" => "zugestellt",
        "nicht_gefunden" => "Ordner nicht gefunden",
        "Zugriff_verweigert" => "kein Schreibzugriff",
        "Profil_abweichend" => "vorhandene Datei weicht ab (nicht überschrieben)",
        "veraltet" => "Profil veraltet – neu erstellen",
        _ => status
    };
    private void BuildCollection()
    {
        collectionPanel.Children.Add(NetworkTransferHint(true));
        Intro(collectionPanel, "Ausgefüllte Kurswahlen in den Lehrerbestand übernehmen", "Das Schülerprogramm speichert die Wahl als aktuell.json im persönlichen Wahlordner. Bei geöffnetem Lehrerprogramm werden neue Wahlen alle 60 Sekunden automatisch eingelesen und gegen die aktuellen Stammdaten geprüft. Sie müssen hier nur eingreifen, wenn ein Konflikt angezeigt wird.");
        collectionPanel.Children.Add(Help("Was ist ein Konflikt?", "Ein Konflikt entsteht, wenn die Beratung die Wahl einer Person bereits geändert hat und danach eine andere Schülerdatei im Wahlordner auftaucht. Das Programm übernimmt die Schülerdatei dann nicht automatisch.\n\n• „Übernehmen“: Die Schülerwahl wird neuer aktueller Stand (Begründung nötig, Häkchen oben setzen).\n• „Verwerfen“: Der Stand der Beratung bleibt; dieselbe Schülerdatei wird nicht erneut gemeldet.\n\nIn beiden Fällen bleibt die Historie vollständig erhalten."));
        collectionPanel.Children.Add(Help("Welche Datei muss ich auswählen?", "Keine. Für jede Person muss lediglich der persönliche Wahlordner hinterlegt und bestätigt sein – das geschieht bei der gemeinsamen Verteilung automatisch. In diesem Ordner wird aktuell.json gesucht.\n\nprofil.json enthält Voraussetzungen, keine gewählten Kurse. Excel-/CSV-Schülerlisten gehören in den Schülerlisten-Import. Ein PDF-Kontrollbogen lässt sich nicht als Kurswahl einlesen."));
        collectionPanel.Children.Add(Button("Jetzt auf neue Kurswahlen prüfen", CollectAutomatically, "Sofort einlesen, ohne auf die automatische Prüfung zu warten."));
        collectionPanel.Children.Add(collectionResults); collectionPanel.Children.Add(conflict); collectionPanel.Children.Add(reason);
        var conflictActions = new WrapPanel { Orientation = Orientation.Horizontal };
        var acceptConflicts = Button("Ausgewählte Konflikte übernehmen (Schülerwahl gilt)", () =>
        {
            Ready(); var results = new List<string>(); var remaining = ImmutableArray.CreateBuilder<CollectionPreview>();
            var chosen = conflictSelection.Count > 0 ? conflictSelection.Select(c => (c.Box.IsChecked == true, c.Preview)).ToArray() : collections.Select(p => (true, p)).ToArray();
            foreach (var (selected, preview) in chosen)
            {
                if (!selected) { remaining.Add(preview); continue; }
                try { session!.Confirm(preview, true, conflict.IsChecked == true, reason.Text ?? ""); results.Add(preview.Student.Student.SyntheticDisplayName + ": übernommen"); }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { remaining.Add(preview); results.Add(preview.Student.Student.SyntheticDisplayName + ": " + ex.Message); }
            }
            collections = remaining.ToImmutable(); conflict.IsChecked = false;
            RenderCollectionStatus(0, results); Refresh(); status.Text = string.Join(" · ", results);
        }, "Übernimmt die ausgewählten Schülerwahlen als neuen aktuellen Stand. Häkchen oben und Begründung sind erforderlich.");
        acceptConflicts.Margin = new Thickness(0, 0, 10, 0);
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
            RenderCollectionStatus(0, []); Refresh(); status.Text = $"{dismissed} Konflikte verworfen; der Stand der Beratung bleibt gültig.";
        }, "Behält den aktuellen Lehrerbestand bei und meldet dieselbe Schülerdatei nicht erneut.");
        conflictActions.Children.Add(acceptConflicts); conflictActions.Children.Add(dismissButton);
        collectionPanel.Children.Add(conflictActions);
    }
    private void BuildCatalog()
    {
        catalogPanel.Children.Clear(); if (session is null) return;
        Intro(catalogPanel, "Fachangebot Ihres Jahrgangs", "Diese Fächer, Kursarten und Halbjahre sieht das Schülerprogramm. Das Fachangebot wird beim Verteilen der Schülerdateien automatisch in jeden Wahlordner kopiert. Änderungen nach Eingang der ersten Wahlen nur über Kursplanung → Angebot ändern.");
        catalogPanel.Children.Add(Text($"Abitur {session.Catalog.GraduationYear} · Angebotsstand {session.Catalog.Version}"));
        const string columns = "220,110,90,160,*";
        catalogPanel.Children.Add(new Border { Background = SoftAccent, Child = CompactLine(columns, [Text("Fach"), Text("Kursart"), Text("Variante"), Text("Status"), Text("Halbjahre · Wochenstunden")]) });
        var index = 0;
        foreach (var offer in session.Catalog.Offers.OrderBy(o => SubjectName(o.SubjectId), StringComparer.CurrentCulture).ThenBy(o => o.Type))
        {
            var hours = string.Join(", ", offer.Schedule.Select(h => $"HJ {h.Semester}: {h.WeeklyHours} h"));
            catalogPanel.Children.Add(new Border { Background = index++ % 2 == 0 ? Brush.Parse("#F1F5F7") : Brushes.White,
                Child = CompactLine(columns, [Text(SubjectName(offer.SubjectId)), Text(FormatCourseType(offer.Type)), Text(FormatVariant(offer.Variant)), Text(FormatOfferStatus(offer.Status)), Text(hours)]) });
        }
        catalogPanel.Children.Add(Button("Fachangebot als Datei speichern …", ExportCurrentCatalog, "Nur für Sonderfälle, z. B. eine zentrale Schülerinstallation. Beim normalen Verteilen wird das Fachangebot automatisch mitkopiert."));
    }
}
