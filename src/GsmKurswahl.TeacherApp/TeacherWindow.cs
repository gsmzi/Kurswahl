using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GsmKurswahl.Application;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Domain;
using GsmKurswahl.ImportExport;
using GsmKurswahl.Storage;

namespace GsmKurswahl.TeacherApp;

public sealed class TeacherWindow : Window
{
    private IAdministrationStore? store;
    private TeacherSession? session;
    private readonly TextBlock status = new() { Text = "Lokalen Verwaltungsbestand und Jahrgangskatalog öffnen. Nur synthetische Daten verwenden.", TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel roster = new() { Spacing = 8 };
    private readonly StackPanel detail = new() { Spacing = 8 };
    private readonly StackPanel importPanel = new() { Spacing = 8 };
    private readonly StackPanel profilePanel = new() { Spacing = 8 };
    private readonly StackPanel collectionPanel = new() { Spacing = 8 };
    private readonly StackPanel catalogPanel = new() { Spacing = 8 };
    private readonly TextBox filter = new() { Watermark = "Name, Klasse oder Schüler-ID" };
    private readonly CheckBox followupFilter = new() { Content = "Nur Nachwahl" };
    private readonly ComboBox statusFilter = new() { ItemsSource = new[] { "Alle Prüfstände", "Ohne Wahl", "invalid", "needs_review", "valid" }, SelectedIndex = 0 };
    private readonly TextBox actor = new() { Text = "demo-beratung", Watermark = "Bearbeiterkennung" };
    private string Actor => actor.Text?.Trim() is { Length: > 0 } value ? value : throw new ArgumentException("Bearbeiterkennung fehlt.");
    private readonly ImportColumnPicker mapping = new();
    private ImportTable? table;
    private ImportOrigin? importOrigin;
    private ImmutableArray<StudentImportRow> importRows = [];
    private readonly StackPanel importResults = new() { Spacing = 5 };
    private ImmutableArray<CollectionPreview> collections = [];
    private readonly StackPanel collectionResults = new() { Spacing = 5 };
    private readonly CheckBox conflict = new() { Content = "Angezeigte Konflikte bewusst zugunsten der eingelesenen Wahl entscheiden" };
    private readonly TextBox reason = new() { Watermark = "Änderungs-/Übernahmegrund" };
    private readonly TextBox thumbprint = new() { Watermark = "Fingerabdruck des P-256-Signierzertifikats (Windows: eigener Zertifikatspeicher)" };
    private readonly TextBox keyId = new() { Watermark = "Öffentliche Schlüsselkennung, z. B. demo-key-01" };
    private readonly ListBox profileTargets = new() { SelectionMode = SelectionMode.Multiple, MinHeight = 180 };
    private sealed record Target(StudentRecord Record) { public override string ToString() => $"{Record.Student.ExternalStudentId} · {Record.WindowsUsername} → {Record.HomeDirectory} · Zuordnung {(Record.MappingConfirmed ? "bestätigt" : "offen")}"; }

    public TeacherWindow()
    {
        Title = "GSM-Kurswahl | Lehrerverwaltung · M3"; Width = 1400; Height = 900; MinWidth = 900; MinHeight = 600;
        var root = new DockPanel { Margin = new Thickness(20) };
        var top = new StackPanel { Spacing = 8 };
        top.Children.Add(new TextBlock { Text = "Lehrerverwaltung", FontSize = 26 });
        top.Children.Add(new TextBlock { Text = "Synthetischer Erprobungsbetrieb · fachliche Freigabe Abitur 2029 offen · keine verbindliche Abgabe", TextWrapping = TextWrapping.Wrap });
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        actor.Width = 220; toolbar.Children.Add(actor); toolbar.Children.Add(Button("Verwaltung öffnen …", Open, "Zwei Schritte: zuerst das Fachangebot (.json), danach die lokale Verwaltungsdatei (.db) auswählen oder neu anlegen. Eine Schülerliste laden Sie anschließend unter Schülerlisten.")); top.Children.Add(toolbar);
        top.Children.Add(status); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var tabs = new TabControl();
        var people = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,3*") };
        var left = new StackPanel { Spacing = 8 }; left.Children.Add(filter); left.Children.Add(followupFilter); left.Children.Add(statusFilter); left.Children.Add(roster);
        people.Children.Add(Scroll(left)); var right = Scroll(detail); Grid.SetColumn(right, 1); people.Children.Add(right);
        tabs.ItemsSource = new[] { Tab("Start & Dateien", Scroll(BuildOrientation())), Tab("Schülerbestand", people), Tab("Schülerlisten", Scroll(importPanel)), Tab("Profile ausgeben", Scroll(profilePanel)), Tab("Wahlen einsammeln", Scroll(collectionPanel)), Tab("Fachangebot", Scroll(catalogPanel)) };
        root.Children.Add(tabs); Content = root;
        filter.TextChanged += (_, _) => Refresh(); followupFilter.IsCheckedChanged += (_, _) => Refresh(); statusFilter.SelectionChanged += (_, _) => Refresh();
        Closed += (_, _) => store?.Dispose();
        BuildImport(); BuildProfiles(); BuildCollection();
        catalogPanel.Children.Add(Text("Hier wählen Sie Jahrgang und Leistungsfächer für eine neue Angebotsdatei. Öffnen Sie dafür zuerst oben die Verwaltung mit einem vorhandenen Fachangebot."));
    }
    private static TabItem Tab(string name, Control content) => new() { Header = name, Content = content };
    private static ScrollViewer Scroll(Control content) => new() { Content = content, Margin = new Thickness(8) };
    private Button Button(string label, Func<Task> action, string? help = null)
    {
        var button = new Button { Content = label };
        if (help is not null) ToolTip.SetTip(button, new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
        button.Click += async (_, _) => { button.IsEnabled = false; try { await action(); } catch (Exception ex) { status.Text = "Vorgang nicht abgeschlossen: " + ex.Message; } finally { button.IsEnabled = true; } };
        return button;
    }
    private Button Button(string label, Action action, string? help = null) => Button(label, () => { action(); return Task.CompletedTask; }, help);
    private static TextBlock Text(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap };
    private static Expander Help(string title, string explanation) => new()
    { Header = title, Content = new TextBlock { Text = explanation, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12), MaxWidth = 850 }, HorizontalAlignment = HorizontalAlignment.Stretch };
    private static void Intro(StackPanel panel, string heading, string explanation)
    {
        panel.Children.Add(new TextBlock { Text = heading, FontSize = 21, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(Text(explanation));
    }
    private static StackPanel BuildOrientation()
    {
        var panel = new StackPanel { Spacing = 14, MaxWidth = 950, HorizontalAlignment = HorizontalAlignment.Left };
        Intro(panel, "Welche Datei brauche ich wofür?", "Im Alltag gibt es drei Aufgaben: Schülerlisten einlesen, persönliche Profile ausgeben und ausgefüllte Kurswahlen einsammeln. Die Dateiendung allein sagt nicht, welchen Inhalt eine Datei hat.");
        Intro(panel, "1 · Verwaltung öffnen", "Oben „Verwaltung öffnen …“ wählen. Zuerst das Fachangebot auswählen (welche Kurse angeboten werden), danach die Verwaltungsdatei (Ihr gespeicherter Lehrerbestand). Beim ersten Start eine neue Verwaltungsdatei anlegen; später dieselbe wieder öffnen.");
        Intro(panel, "2 · Schülerliste einlesen", "Unter „Schülerlisten“ eine Excel- oder CSV-Tabelle mit Personen, Klassen und Vorbildung laden. Spalten über Auswahllisten zuordnen, Vorschau prüfen und gewünschte Zeilen übernehmen. Danach im Schülerbestand Konto und persönlichen Wahlordner bestätigen.");
        Intro(panel, "3 · Profile ausgeben", "Ein Profil enthält die bestätigten Voraussetzungen einer Person, etwa IMP oder bilingualen Bildungsgang. Unter „Profile ausgeben“ erstellt und signiert das Lehrerprogramm die Profile und legt sie in den persönlichen Wahlordnern ab. Die Signatur ist das digitale Echtheitssiegel. Dafür richtet die Schul-IT einmalig den Signierschlüssel ein.");
        Intro(panel, "4 · Kurswahlen einsammeln", "Wenn die Schüler ihre Kurse gewählt haben: unter „Wahlen einsammeln“ die Vorschau lesen. Die Dateien werden automatisch in den bestätigten Wahlordnern gefunden. Nach Prüfung übernehmen Sie die Wahlen in Ihren Lehrerbestand.");
        panel.Children.Add(Help("Dateiübersicht: Inhalt, Herkunft und Verwendung", "Schülerliste (.xlsx / .csv)\nEine Tabelle aus Ihrer vorbereiteten Klassen-10-Liste. Unter Schülerlisten einlesen. Enthält Personen und Voraussetzungen.\n\nFachangebot (.json), z. B. fachangebot-demo.json\nBeschreibt Fächer, Kursarten und Halbjahre eines Jahrgangs. Beim Öffnen der Verwaltung auswählen; unter Fachangebot eine neue Version vorbereiten.\n\nVerwaltungsdatei (.db), z. B. verwaltung.db\nSpeichert den Lehrerbestand mit Änderungen und Historie lokal. Beim ersten Start anlegen und später wieder öffnen.\n\nPersönliches Profil (profil.json) + Echtheitssiegel (profil.sig)\nDas Lehrerprogramm erzeugt beide gemeinsam aus den geprüften Voraussetzungen. Unter Profile ausgeben verteilen. Diese beiden Dateien sind keine Schülerliste und keine Kurswahl.\n\nKurswahl (aktuell.json)\nWird vom Schülerprogramm gespeichert und enthält die gewählten Kurse. Unter Wahlen einsammeln automatisch aus dem persönlichen Wahlordner lesen.\n\nKontrollbogen (.pdf)\nLesbare Übersicht aus dem Schülerprogramm. Zum Ansehen und Kontrollieren; kein Importformat.\n\nASV-Dateien\nEin eigener ASV-Import ist noch nicht verfügbar."));
        panel.Children.Add(Help("Mit welchen Testdateien kann ich beginnen?", "Im Projektordner samples liegen:\n• fachangebot-demo.json: beim Öffnen der Verwaltung als Fachangebot wählen.\n• klasse10-demo.csv: unter Schülerlisten laden; enthält zwei erfundene Personen.\n• schueler-demo.json: Beispiel für eine Kurswahl. Beim Einsammeln erwartet das Programm den Namen aktuell.json im bestätigten Wahlordner.\n• profil-demo-unsigned.json: erklärt nur das Profilformat. Es ist absichtlich unsigniert und kein nutzbarer Berechtigungsnachweis.\n\nEine .db-Datei legen Sie beim ersten Öffnen selbst an. Für einen ersten Blick auf den Import benötigen Sie noch keine signierten Profile."));
        panel.Children.Add(Help("Nur für die Einrichtung: Schlüssel und Einstellungen", "profile-trust.json enthält die zugelassenen öffentlichen Prüfschlüssel und Kontodomänen der geschützten Schülerinstallation. Der Export public-profile-key.json liefert einen öffentlichen Schlüssel für diese Einrichtung. appsettings.json enthält Programmeinstellungen. Diese Dateien richten Verwaltung und Schul-IT ein; sie werden nicht als Schülerliste importiert. Der private Signierschlüssel bleibt im administrativen Windows-Zertifikatspeicher."));
        return panel;
    }
    private static string Describe(StudentRecord r)
    {
        var p = r.Student.Prerequisites;
        return $"{r.Student.SyntheticDisplayName}, {r.Student.ClassName}, Abitur {r.Student.GraduationYear}, Abgängerstatus {r.Student.Departure}\nKonto: {r.WindowsUsername}; SID: {r.AccountId}; Ziel: {r.HomeDirectory}\nItalienisch: {p.ItalianProfile}; Brücke: {p.InformaticsBridge}; IMP: {p.InformaticsImp}; Bili: {p.ContinuousBilingual}\nReligion/Ethik Klasse 10: {p.ReligionEthicsGrade10 ?? "unbekannt"}; Belegung freigegeben: {p.ReligionEthicsPermission}; Prüfung freigegeben: {p.ReligionEthicsExamPermission}\nSportbefreiung: {(p.SportExemptSemesters is { } h ? (h.Length == 0 ? "keine" : string.Join(',', h)) : "unbekannt")}; dauerhaft: {p.PermanentSportExemption}; bestätigte Sprachlaufbahnen: {string.Join(',', p.ContinuedLanguages)}";
    }
    private async Task<string?> Pick(string title, string typeName, params string[] patterns)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = false, FileTypeFilter = [new FilePickerFileType(typeName) { Patterns = patterns }] });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
    private async Task Open()
    {
        var catalogPath = await Pick("Schritt 1 von 2: Fachangebot auswählen (z. B. fachangebot-demo.json)", "Fachangebot (.json)", "*.json"); if (catalogPath is null) return;
        var catalog = JsonDocuments.ReadCatalog(await File.ReadAllBytesAsync(catalogPath));
        if (!catalog.IsDemo) throw new ArgumentException("Dieser Entwicklungsstand benötigt einen synthetischen Demo-Katalog.");
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Schritt 2 von 2: Verwaltungsdatei öffnen oder neu anlegen", SuggestedFileName = "verwaltung.db", DefaultExtension = "db", FileTypeChoices = [new FilePickerFileType("Lehrerbestand (.db)") { Patterns = ["*.db"] }], ShowOverwritePrompt = false });
        var path = file?.TryGetLocalPath(); if (path is null) return;
        store?.Dispose(); store = null; session = null;
        store = new SqliteAdministrationStore(path); session = new TeacherSession(store, catalog, Actor); actor.IsReadOnly = true;
        status.Text = $"Verwaltungsbestand geöffnet · Abitur {catalog.GraduationYear} · Angebot {catalog.Version}";
        collections = []; collectionResults.Children.Clear(); detail.Children.Clear();
        importRows = []; importOrigin = null; table = null; mapping.Clear(); importResults.Children.Clear();
        BuildCatalog(); Refresh();
    }
    private void Ready() { if (store is null || session is null) throw new InvalidOperationException("Zuerst Bestand und Katalog öffnen."); }
    private void Refresh()
    {
        if (session is null || store is null) return;
        roster.Children.Clear();
        foreach (var state in session.Overview().Where(s => s.Record.Student.GraduationYear == session.Catalog.GraduationYear))
        {
            var label = $"{state.Record.Student.SyntheticDisplayName} · {state.Record.Student.ClassName} · {state.Record.Student.ExternalStudentId}";
            if (!label.Contains(filter.Text ?? "", StringComparison.OrdinalIgnoreCase) || followupFilter.IsChecked == true && !state.RequiresReselection) continue;
            if (statusFilter.SelectedIndex == 1 && state.Active is not null || statusFilter.SelectedIndex == 2 && state.Validation?.Status != ReviewStatus.Invalid
                || statusFilter.SelectedIndex == 3 && state.Validation?.Status != ReviewStatus.NeedsReview || statusFilter.SelectedIndex == 4 && state.Validation?.Status != ReviewStatus.Valid) continue;
            roster.Children.Add(Button(label + $"\n{state.Validation?.Status.ToString() ?? "Ohne Wahl"} · {(state.RequiresReselection ? "Nachwahl nötig" : "keine Nachwahl markiert")}", () => ShowStudent(state)));
        }
        profileTargets.ItemsSource = store.Students().Where(s => s.Student.GraduationYear == session.Catalog.GraduationYear).Select(s => new Target(s)).ToArray();
    }
    private void ShowStudent(TeacherState state)
    {
        Ready(); detail.Children.Clear(); var record = state.Record; var id = record.Student.ExternalStudentId;
        detail.Children.Add(Text($"{record.Student.SyntheticDisplayName} · Stammdatenrevision {record.Version}"));
        var user = new TextBox { Text = record.WindowsUsername, Watermark = "DOMÄNE\\Konto" };
        var sid = new TextBox { Text = record.AccountId, Watermark = "Administrativ geprüfte SID" };
        var home = new TextBox { Text = record.HomeDirectory, Watermark = "Absolutes persönliches Wahl-/Profilverzeichnis" };
        ToolTip.SetTip(user, "Das schulische Windows-Konto dieser Person. Es wird mit dem persönlichen Profil abgeglichen.");
        ToolTip.SetTip(sid, "SID = eindeutige Windows-Konto-ID. Die Schul-IT bestätigt diese Zuordnung; der Benutzername allein reicht nicht aus.");
        ToolTip.SetTip(home, "Hier liegen die Kurswahl aktuell.json sowie das Profil profil.json mit profil.sig. Das Verzeichnis muss bereits vorhanden sein und eindeutig dieser Person gehören.");
        var confirmed = new CheckBox { Content = "Schüler-ID, Konto, SID und tatsächliches Zielverzeichnis administrativ geprüft", IsChecked = record.MappingConfirmed };
        detail.Children.Add(user); detail.Children.Add(sid); detail.Children.Add(home); detail.Children.Add(confirmed);
        detail.Children.Add(Button("Zuordnung speichern", () =>
        {
            if (confirmed.IsChecked == true && (string.IsNullOrWhiteSpace(sid.Text) || (user.Text ?? "").Split('\\').Length != 2 || !Path.IsPathFullyQualified(home.Text ?? ""))) throw new ArgumentException("Vollständige Konto-/SID-/Home-Zuordnung erforderlich.");
            store!.SaveStudent(record with { WindowsUsername = user.Text ?? "", AccountId = sid.Text ?? "", HomeDirectory = string.IsNullOrWhiteSpace(home.Text) ? "" : Path.GetFullPath(home.Text), MappingConfirmed = confirmed.IsChecked == true, Version = record.Version + 1, Actor = Actor, ChangedAt = DateTimeOffset.UtcNow }, record.Version);
            Refresh(); detail.Children.Clear(); status.Text = "Zuordnung versioniert gespeichert. Person erneut öffnen.";
        }));
        detail.Children.Add(Text($"Vorbildung: INF-Brücke {record.Student.Prerequisites.InformaticsBridge}, IMP {record.Student.Prerequisites.InformaticsImp}, Italienisch {record.Student.Prerequisites.ItalianProfile}, Bili {record.Student.Prerequisites.ContinuousBilingual}. Korrekturen über geprüfte Importzeile; bestehende Personen werden nur einzeln bestätigt geändert."));
        if (state.Validation is { } validation) foreach (var message in validation.Messages) detail.Children.Add(Text(message.Rule.Id + ": " + message.Explanation));
        var editReason = new TextBox { Watermark = "Begründung für Änderung/Wiederherstellung/Nachwahl" }; detail.Children.Add(editReason);
        var deadline = new TextBox { Watermark = "Nachwahlfrist (JJJJ-MM-TT)", Text = state.ReselectionDeadline?.ToString("yyyy-MM-dd") }; detail.Children.Add(deadline);
        detail.Children.Add(Button("Nachwahl vormerken", () => { session!.FollowUp(id, true, string.IsNullOrWhiteSpace(deadline.Text) ? null : DateTimeOffset.ParseExact(deadline.Text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), editReason.Text ?? ""); Refresh(); status.Text = "Nachwahl protokolliert."; }));
        detail.Children.Add(Button("Nachwahl abschließen", () => { session!.FollowUp(id, false, null, editReason.Text ?? ""); Refresh(); }));
        var active = state.Active;
        var choice = active?.Snapshot ?? new CourseChoice(2, Guid.NewGuid(), id, record.Student.GraduationYear, session!.Catalog.Version, GsmKurswahl.Rules.ChoiceValidator.Version, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, SubmissionStatus.Draft, ElectionPhase.Preliminary, [], []);
        detail.Children.Add(Text("Administrative Wahl bearbeiten · angebotene Halbjahre wählen"));
        var selections = new List<(CourseOffer Offer, int Semester, CheckBox Box)>();
        foreach (var offer in session!.Catalog.Offers)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            line.Children.Add(new TextBlock { Text = $"{offer.SubjectId} {offer.Type} {offer.Variant}", Width = 190 });
            foreach (var semester in offer.Schedule)
            {
                var box = new CheckBox { Content = "HJ " + semester.Semester, IsChecked = choice.Selections.Any(s => s.OfferId == offer.Id && s.Semesters.Contains(semester.Semester)) };
                line.Children.Add(box); selections.Add((offer, semester.Semester, box));
            }
            detail.Children.Add(line);
        }
        var oral = new TextBox { Text = string.Join(",", choice.PlannedOralSubjects), Watermark = "Mündliche Planung: zwei Fachkennungen, durch Komma getrennt" }; detail.Children.Add(oral);
        detail.Children.Add(Button("Lehreränderung als neue Revision speichern", () =>
        {
            var selected = selections.Where(s => s.Box.IsChecked == true).GroupBy(s => s.Offer).Select(g => new CourseSelection(g.Key.Id, g.Key.SubjectId, g.Key.Type, g.Key.Variant, [.. g.Select(v => v.Semester)])).ToImmutableArray();
            session.Save(choice with { Selections = selected, PlannedOralSubjects = [.. (oral.Text ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)], UpdatedAt = DateTimeOffset.UtcNow }, active?.Id, RevisionSource.TeacherEdit, editReason.Text ?? "");
            Refresh(); detail.Children.Clear(); status.Text = "Neue Lehrerrevision gespeichert und gegen aktuelle Stammdaten geprüft.";
        }));
        detail.Children.Add(Text("Wahlhistorie (vollständige Snapshots; Wiederherstellung erzeugt neue Revision)"));
        foreach (var revision in store!.History(id).Reverse())
        {
            detail.Children.Add(Text($"{revision.ChangedAt:g} · {revision.Source} · {revision.ActorId} · {revision.Reason}\n" + string.Join("; ", revision.Changes.Select(c => $"{c.OfferId}: [{string.Join(',', c.Before)}] → [{string.Join(',', c.After)}]"))));
            detail.Children.Add(Button("Diesen Stand wiederherstellen", () => { session.Restore(id, revision.Id, active!.Id, editReason.Text ?? ""); Refresh(); detail.Children.Clear(); status.Text = "Neue Wiederherstellungsrevision angelegt."; }));
        }
    }
    private void BuildImport()
    {
        Intro(importPanel, "Schüler und Vorbildung aus einer Tabelle übernehmen", "Hier laden Sie eine Klassen-10-Liste mit Namen, Klassen, Benutzerkonten und persönlichen Voraussetzungen. Anschließend ordnen Sie die Tabellenspalten zu und prüfen die Vorschau. Erst Ihre Bestätigung übernimmt eine Person in den Lehrerbestand.");
        importPanel.Children.Add(Help("Welche Tabelle kann ich verwenden?", "Excel (.xlsx): genau ein Tabellenblatt mit einer Kopfzeile und festen Werten; Formeln vorher als Werte speichern.\nCSV (.csv): Texttabelle mit Kopfzeile, Semikolon als Trennzeichen und UTF-8-Zeichencodierung.\n\nZum Ausprobieren: samples/klasse10-demo.csv. Profil-, Angebots- und Kurswahldateien (.json) gehören nicht in diesen Import. Ein spezieller ASV-Adapter ist noch nicht verfügbar."));
        importPanel.Children.Add(Button("1 · Schülerliste auswählen (.xlsx / .csv) …", async () =>
        {
            Ready(); var path = await Pick("Schülerliste mit Personen und Vorbildung auswählen", "Schülerlisten (Excel oder CSV)", "*.xlsx", "*.csv"); if (path is null) return;
            if (Path.GetExtension(path).ToLowerInvariant() is not (".csv" or ".xlsx")) throw new ArgumentException("Bitte eine Schülerliste als .xlsx oder .csv auswählen. Kurswahlen werden unter Wahlen einsammeln gelesen.");
            if (new FileInfo(path).Length > 16_000_000) throw new InvalidDataException("Importdatei überschreitet 16 MB.");
            var bytes = await File.ReadAllBytesAsync(path);
            table = Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ? StudentTableImport.Xlsx(new MemoryStream(bytes)) : StudentTableImport.Csv(Encoding.UTF8.GetString(bytes));
            importOrigin = new(Path.GetFileName(path), Convert.ToHexString(SHA256.HashData(bytes)), "m3-table-v1", DateTimeOffset.UtcNow, Actor);
            mapping.Load(table);
            importRows = []; importResults.Children.Clear(); status.Text = "Spalten: " + string.Join(", ", table.Headers);
        }, "Öffnet eine Excel- oder CSV-Tabelle. Das Laden allein verändert noch keine Stammdaten."));
        importPanel.Children.Add(Text("2 · Spalten zuordnen: Wählen Sie rechts die passende Spalte Ihrer Tabelle. * = Pflichtfeld. Optionale Voraussetzungen ohne Zuordnung bleiben unbekannt; ein fehlender Abgängerstatus wird als aktiv behandelt."));
        importPanel.Children.Add(Help("Was bedeuten die Angaben?", "Schüler-ID: eindeutige Kennung einer Person. Windows-Benutzername: ihr Schulkonto. SID: eindeutige technische Konto-ID, von der Schul-IT zu bestätigen. Persönlicher Wahlordner: enthält später Profil und Kurswahl.\n\nVoraussetzungen wie IMP oder bilingual: ja/nein; leer bedeutet unbekannt. Sportbefreiung: z. B. 1,2 für die betroffenen Halbjahre, keine für bestätigte Nichtbefreiung. Sprachen: bestätigte Fachkennungen wie E,F. Religion/Ethik: ETH, ER oder KR.\n\nWeitere Erläuterungen stehen als Tooltip direkt an jedem Feld. Unbekannte Angaben sind weder erfüllt noch falsch."));
        importPanel.Children.Add(mapping);
        importPanel.Children.Add(Button("3 · Vorschau prüfen", () =>
        {
            Ready(); if (table is null || importOrigin is null) throw new InvalidOperationException("Importdatei fehlt.");
            var map = mapping.Read();
            var mappingHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", map.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)))));
            importRows = StudentTableImport.Preview(table, map, importOrigin with { MappingVersion = "m3-table-v1:" + mappingHash }, Actor); importResults.Children.Clear();
            foreach (var row in importRows)
            {
                importResults.Children.Add(Text($"Zeile {row.Row}: {row.Candidate?.Student.ExternalStudentId} · {row.Candidate?.Student.SyntheticDisplayName} · {row.Error}"));
                if (row.Error.Length > 0 || row.Candidate is not { } candidate) continue;
                var current = store!.Students().SingleOrDefault(s => s.Student.ExternalStudentId == candidate.Student.ExternalStudentId);
                if (current is not null) importResults.Children.Add(Text("Aktueller geschützter Bestand:\n" + Describe(current)));
                importResults.Children.Add(Text("Vorgeschlagene Übernahme:\n" + Describe(candidate)));
                importResults.Children.Add(Text($"{(current is null ? "NEU" : "BESTEHEND – Einzelentscheidung")} · {candidate.WindowsUsername} · Klasse {candidate.Student.ClassName} · Abitur {candidate.Student.GraduationYear}\nVorbildung: Italienisch {candidate.Student.Prerequisites.ItalianProfile}, Brücke {candidate.Student.Prerequisites.InformaticsBridge}, IMP {candidate.Student.Prerequisites.InformaticsImp}, Bili {candidate.Student.Prerequisites.ContinuousBilingual}"));
                importResults.Children.Add(Button(current is null ? "Diese neue Person bestätigt übernehmen" : "Diese Stammdatenkorrektur bestätigt übernehmen", () =>
                {
                    if (candidate.Student.GraduationYear != session!.Catalog.GraduationYear) throw new ArgumentException("Jahrgang passt nicht zum geöffneten Katalog.");
                    var changed = StudentImportService.Confirm(store, candidate, current?.Version, true);
                    Refresh(); status.Text = changed ? "Stammdaten übernommen; Kontozuordnung erneut prüfen. Wahlen und Historien bleiben erhalten und werden neu bewertet." : "Identischer Import bereits übernommen; bestehende Korrekturen bleiben erhalten.";
                }));
            }
        }, "Prüft Ihre Spaltenzuordnung und zeigt neue Personen, Änderungen und Fehler. Danach bestätigen Sie jede gewünschte Zeile einzeln."));
        importPanel.Children.Add(importResults);
    }
    private void BuildProfiles()
    {
        Intro(profilePanel, "Persönliche Voraussetzungen an die Schülerprogramme weitergeben", "Ein Profil sagt dem Schülerprogramm, welche Voraussetzungen bestätigt sind – etwa IMP oder bilingualer Bildungsgang. Das Lehrerprogramm erstellt profil.json und das zugehörige digitale Echtheitssiegel profil.sig. Die beiden Dateien werden gemeinsam ausgegeben.");
        profilePanel.Children.Add(Help("Wann brauche ich eine neue Ausgabe?", "Beim ersten Mal: Personen auswählen → Profile erstellen und signieren → an Wahlordner verteilen. Konto und Wahlordner müssen zuvor im Schülerbestand geprüft und bestätigt sein.\n\nNach einer Vorbildungskorrektur: ein neues Profil erstellen und anschließend verteilen.\n\nNach einem Zustellfehler: nur die Verteilung wiederholen. Dafür braucht es keine neue Profilversion. Die gewählten Kurse stehen separat in aktuell.json und bleiben erhalten."));
        profilePanel.Children.Add(Text("Personen auswählen · Strg/Umschalt für mehrere Personen"));
        profilePanel.Children.Add(profileTargets);
        var setup = new StackPanel { Spacing = 8 };
        setup.Children.Add(Text("Einmalige Einrichtung mit der Schul-IT: Zum Signieren wird ein administrativ bereitgestelltes Zertifikat benötigt. Der Fingerabdruck identifiziert dieses Zertifikat; die Schlüsselkennung benennt seinen öffentlichen Prüfschlüssel."));
        setup.Children.Add(Text("Zertifikat-Fingerabdruck")); setup.Children.Add(thumbprint);
        ToolTip.SetTip(thumbprint, "Kennung des bereitgestellten P-256-Zertifikats im Windows-Zertifikatspeicher. Dies ist kein Passwort und keine Schüler-ID.");
        setup.Children.Add(Text("Schlüsselkennung")); setup.Children.Add(keyId);
        ToolTip.SetTip(keyId, "Muss der Schlüsselkennung in der geschützten Schülerinstallation entsprechen, z. B. demo-key-01. Mit der Schul-IT abstimmen.");
        setup.Children.Add(Button("Öffentlichen Prüfschlüssel für die Schülerinstallation exportieren", async () =>
        {
            using var certificates = new X509Store(StoreName.My, StoreLocation.CurrentUser); certificates.Open(OpenFlags.ReadOnly);
            var matches = certificates.Certificates.Find(X509FindType.FindByThumbprint, thumbprint.Text ?? "", false);
            if (matches.Count != 1 || string.IsNullOrWhiteSpace(keyId.Text)) throw new InvalidOperationException("Eindeutiges Zertifikat und Schlüsselkennung erforderlich.");
            using var certificate = matches[0]; using var key = certificate.GetECDsaPublicKey() ?? throw new InvalidOperationException("ECDSA-Schlüssel fehlt.");
            if (key.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7") throw new InvalidOperationException("P-256-Schlüssel erforderlich.");
            var exported = new TrustedProfileKey(keyId.Text, Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Öffentlichen Schlüssel speichern", SuggestedFileName = "public-profile-key.json" });
            var path = file?.TryGetLocalPath(); if (path is null) return;
            using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            output.Write(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(exported, DocumentJson.Options)); output.Flush(true);
            status.Text = "Öffentlicher Schlüssel exportiert. Administrativ in keys der geschützten profile-trust.json aufnehmen und Kontodomäne prüfen.";
        }, "Einrichtungshilfe für die Schul-IT: exportiert nur den öffentlichen Prüfschlüssel. Diese Datei enthält keine Schülerdaten und ist kein Schülerprofil."));
        profilePanel.Children.Add(new Expander { Header = "Signierung einrichten · Schul-IT", Content = setup, HorizontalAlignment = HorizontalAlignment.Stretch });
        profilePanel.Children.Add(Button("1 · Neue Profile erstellen und signieren", () =>
        {
            Ready(); using var certificates = new X509Store(StoreName.My, StoreLocation.CurrentUser); certificates.Open(OpenFlags.ReadOnly);
            var matches = certificates.Certificates.Find(X509FindType.FindByThumbprint, thumbprint.Text ?? "", false);
            if (matches.Count != 1) throw new InvalidOperationException("Signierzertifikat nicht eindeutig gefunden.");
            using var certificate = matches[0]; using var key = certificate.GetECDsaPrivateKey() ?? throw new InvalidOperationException("ECDSA-Privatschlüssel nicht verfügbar.");
            var distribution = new ProfileDistribution(store!); var count = 0; var failed = new List<string>();
            foreach (var target in profileTargets.SelectedItems!.Cast<Target>())
            {
                try { distribution.Issue(target.Record, key, keyId.Text ?? ""); count++; }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or CryptographicException)
                {
                    failed.Add(target.Record.Student.ExternalStudentId + ": " + ex.Message);
                    store!.LogDistribution(new(Guid.NewGuid(), target.Record.Student.ExternalStudentId, 0, DateTimeOffset.UtcNow, "Profil_abweichend", "Signierung fehlgeschlagen; Zuordnung oder Schlüssel prüfen."));
                }
            }
            status.Text = $"{count} neue Einzelprofile signiert. Noch nicht verteilt. " + string.Join(" · ", failed);
        }, "Erstellt für die ausgewählten Personen jeweils eine neue Profilversion mit digitalem Echtheitssiegel. Die Dateien sind danach vorbereitet, aber noch nicht im Schülerordner."));
        profilePanel.Children.Add(Button("2 · Profile an Wahlordner verteilen / erneut zustellen", () =>
        {
            Ready(); var distribution = new ProfileDistribution(store!); var reports = new List<string>();
            foreach (var target in profileTargets.SelectedItems!.Cast<Target>())
            {
                var issued = store!.LatestProfile(target.Record.Student.ExternalStudentId);
                if (issued is null) { reports.Add(target.Record.Student.ExternalStudentId + ": noch kein Profil signiert"); continue; }
                var entry = distribution.Distribute(target.Record, issued); reports.Add(entry.StudentId + ": " + entry.Status);
            }
            status.Text = string.Join(" · ", reports);
        }, "Kopiert profil.json und profil.sig in die bestätigten Wahlordner. Bei Zustellfehlern dieselbe Ausgabe erneut zustellen; vorhandene Kurswahlen werden nicht verändert."));
        var report = new TextBox { IsReadOnly = true, AcceptsReturn = true, MinHeight = 180 };
        profilePanel.Children.Add(Button("Verteilungsbericht anzeigen", () => { Ready(); report.Text = string.Join(Environment.NewLine, store!.DistributionLog().Select(e => $"{e.At:g} · {e.StudentId} · Profil {e.ProfileRevision} · {e.Status} · {e.Detail}")); })); profilePanel.Children.Add(report);
    }
    private void BuildCollection()
    {
        Intro(collectionPanel, "Ausgefüllte Kurswahlen in den Lehrerbestand übernehmen", "Das Schülerprogramm speichert gewählte Fächer und Halbjahre als aktuell.json. Hier findet das Lehrerprogramm diese Dateien automatisch in den bestätigten persönlichen Wahlordnern. Jede Wahl wird gegen die aktuellen Lehrerstammdaten geprüft.");
        collectionPanel.Children.Add(Help("Welche Datei muss ich auswählen?", "Hier ist keine einzelne Dateiauswahl nötig. Im Schülerbestand muss für jede Person der persönliche Wahlordner hinterlegt und bestätigt sein. In diesem Ordner wird aktuell.json gesucht.\n\nprofil.json und profil.sig enthalten Voraussetzungen und Echtheitssiegel, keine gewählten Kurse. Excel-/CSV-Schülerlisten gehören in den Reiter Schülerlisten. Ein PDF-Kontrollbogen lässt sich nicht als Kurswahl einlesen."));
        collectionPanel.Children.Add(Button("1 · Kurswahlen suchen und Vorschau anzeigen", async () =>
        {
            Ready(); var previews = ImmutableArray.CreateBuilder<CollectionPreview>(); collectionResults.Children.Clear();
            foreach (var record in store!.Students().Where(s => s.Student.GraduationYear == session!.Catalog.GraduationYear))
            {
                try
                {
                    if (!record.MappingConfirmed || !Path.IsPathFullyQualified(record.HomeDirectory)) throw new InvalidOperationException("Kontozuordnung ungeprüft.");
                    var path = Path.Combine(record.HomeDirectory, "aktuell.json");
                    var loaded = await new JsonChoiceStore(path, record.HomeDirectory).LoadAsync() ?? throw new FileNotFoundException("Keine aktuelle Wahl vorhanden.");
                    var preview = session!.Preview(loaded.Choice, record.Student.ExternalStudentId, new(path, loaded.ContentToken, "m3-choice-v1", DateTimeOffset.UtcNow, record.WindowsUsername));
                    previews.Add(preview); collectionResults.Children.Add(Text($"{record.Student.ExternalStudentId}: {preview.Validation.Status} · {(preview.Duplicate ? "bereits übernommen" : "neue Übernahme")} · {preview.Conflict}"));
                    foreach (var message in preview.Validation.Messages) collectionResults.Children.Add(Text(message.Rule.Id + ": " + message.Explanation));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
                { collectionResults.Children.Add(Text(record.Student.ExternalStudentId + ": nicht eingelesen – " + ex.Message)); }
            }
            collections = previews.ToImmutable(); status.Text = $"{collections.Length} Wahlen in Vorschau; noch keine Übernahme.";
        }, "Liest aktuell.json aus den bestätigten Ordnern und zeigt Prüfhinweise und Konflikte. Der Lehrerbestand wird erst nach bestätigter Übernahme geändert."));
        collectionPanel.Children.Add(collectionResults); collectionPanel.Children.Add(conflict); collectionPanel.Children.Add(reason);
        collectionPanel.Children.Add(Button("2 · Angezeigte Wahlen bestätigt übernehmen", () =>
        {
            Ready(); var results = new List<string>();
            foreach (var preview in collections)
            {
                try { session!.Confirm(preview, true, conflict.IsChecked == true, reason.Text ?? ""); results.Add(preview.Choice.StudentId + ": übernommen/bereits vorhanden"); }
                catch (InvalidOperationException ex) { results.Add(preview.Choice.StudentId + ": " + ex.Message); }
            }
            Refresh(); status.Text = string.Join(" · ", results);
        }, "Übernimmt die zuvor angezeigten Kurswahlen mit Herkunft und Historie. Bei Konflikten ist Ihre ausdrückliche Entscheidung erforderlich."));
    }
    private void BuildCatalog()
    {
        catalogPanel.Children.Clear(); if (session is null) return;
        catalogPanel.Children.Add(Text("Angebot vor der Verteilung vorbereiten: Jahrgang und LF auswählen, unter neuer Version in neue Datei speichern. Bestehende Verwaltungswahlen bleiben erhalten. Zur Arbeit mit dem neuen Angebot Bestand und neuen Katalog erneut öffnen."));
        var basis = session.Catalog;
        var year = new NumericUpDown { Minimum = 2000, Maximum = 2200, Value = basis.GraduationYear, FormatString = "0" };
        var version = new TextBox { Watermark = "Neue Angebotsversion (eindeutig)" };
        catalogPanel.Children.Add(year); catalogPanel.Children.Add(version);
        var options = new List<(string Id, CheckBox Box)>();
        foreach (var offer in basis.Offers.Where(o => o.Type == CourseType.Lf))
        {
            var box = new CheckBox { Content = $"{offer.SubjectId} · {offer.Variant}", IsChecked = offer.Status is OfferStatus.Offered or OfferStatus.Established };
            options.Add((offer.Id, box)); catalogPanel.Children.Add(box);
        }
        catalogPanel.Children.Add(Button("Angezeigtes Angebot als neue Katalogdatei speichern", async () =>
        {
            var prepared = CatalogConfiguration.Prepare(basis, (int)(year.Value ?? 0), version.Text ?? "", options.Where(o => o.Box.IsChecked == true).Select(o => o.Id).ToHashSet());
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Neue Katalogdatei", SuggestedFileName = "fachangebot-neu.json" });
            var path = file?.TryGetLocalPath(); if (path is null) return;
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temp, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(prepared, DocumentJson.Options));
                File.Move(temp, path, overwrite: false);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            status.Text = "Neue Katalogversion gespeichert. Bestehende Dateien und aktive Wahlen unverändert.";
        }));
    }
}
