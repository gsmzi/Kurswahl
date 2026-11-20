using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.Storage;

namespace GsmKurswahl.TeacherApp;

public sealed partial class TeacherWindow
{
    private readonly string? explicitWorkspaceRoot;
    private readonly string? transferPackageDirectory;
    private TeacherWorkspace? workspace;
    private readonly ContentControl pageHost = new();
    private readonly StackPanel navigation = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly StackPanel dashboardPanel = new() { Spacing = 16 };
    private readonly StackPanel managePanel = new() { Spacing = 12 };
    private readonly StackPanel settingsPanel = new() { Spacing = 12 };
    private readonly TextBlock heading = new() { FontSize = 26, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock rosterCount = new() { Name = "RosterCount", FontSize = 12, Foreground = Muted };
    private readonly Button rosterResetFilter = new() { Name = "PeopleResetFilters", Content = "Filter zurücksetzen", IsEnabled = false };
    private Control peopleView = null!;
    private Control importView = null!;
    private Control profileView = null!;
    private Control collectionView = null!;
    private Control dashboardView = null!;
    private Control settingsView = null!, manageView = null!, catalogView = null!;
    private string visiblePage = "start";

    // Tests pass an isolated root; package launches preselect the bundled transfer folder; production remembers a local directory.
    public TeacherWindow(string? workspaceRoot = null, string? packageDirectory = null)
    {
        explicitWorkspaceRoot = workspaceRoot;
        transferPackageDirectory = string.IsNullOrWhiteSpace(packageDirectory) ? null : Path.GetFullPath(packageDirectory);
        Title = "GSM-Kurswahl | Lehrerverwaltung · M5"; Width = 1400; Height = 900; MinWidth = 900; MinHeight = 600;
        BuildVisualShell();
        // Split view: list on the left, the selected person on the right (no small bottom drawer).
        var people = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,1.3*") };
        var left = new StackPanel { Spacing = 8 };
        var filterRow = new WrapPanel { Orientation = Orientation.Horizontal };
        filter.Width = 240; filter.Margin = new Thickness(0, 0, 10, 6);
        followupFilter.Margin = new Thickness(0, 0, 10, 6); followupFilter.VerticalAlignment = VerticalAlignment.Center;
        statusFilter.MinWidth = 190; statusFilter.Margin = new Thickness(0, 0, 10, 6);
        ToolTip.SetTip(statusFilter, "Formal gültig: keine offenen Punkte · Fachlich zu klären: Vorbildung oder Sonderfall offen · Regelverstoß: Belegung widerspricht einer Regel · Ohne Wahl: noch keine Kurswahl eingelesen · Religion/Ethik: Wechsel: anderes Fach als in Klasse 10 belegt · Feststellungsprüfung nötig: gewechseltes Fach ist mündlich geplant.");
        rosterResetFilter.Margin = new Thickness(0, 0, 0, 6);
        rosterResetFilter.Click += (_, _) =>
        {
            filter.Text = "";
            followupFilter.IsChecked = false;
            statusFilter.SelectedIndex = 0;
        };
        filterRow.Children.Add(filter); filterRow.Children.Add(followupFilter); filterRow.Children.Add(statusFilter); filterRow.Children.Add(Compact(rosterResetFilter));
        var religionExport = Compact(Button("Liste Religion/Ethik-Wechsel und Feststellungsprüfungen (XLSX) …", ExportReligionCases,
            "Alle Personen, die ein anderes Fach als in Klasse 10 belegen, mit Kennzeichnung, wer es mündlich plant und deshalb zu Beginn von HJ 1 eine Feststellungsprüfung braucht."));
        religionExport.Name = "ReligionCasesExport"; religionExport.Margin = new Thickness(0, 0, 0, 4);
        left.Children.Add(filterRow); left.Children.Add(rosterCount); left.Children.Add(religionExport); left.Children.Add(roster);
        var leftScroll = Scroll(left); Grid.SetColumn(leftScroll, 0); people.Children.Add(leftScroll);
        personEditor.Content = Scroll(detail);
        Grid.SetColumn(personEditor, 2); people.Children.Add(personEditor);
        peopleView = people; importView = Scroll(importPanel); profileView = Scroll(profilePanel); collectionView = Scroll(collectionPanel); dashboardView = Scroll(dashboardPanel);
        settingsView = Scroll(settingsPanel); manageView = Scroll(managePanel); catalogView = Scroll(catalogPanel);
        BuildImport(); BuildProfiles(); BuildCollection();
        collectionTimer.Tick += async (_, _) => await CollectAutomatically();
        filter.TextChanged += (_, _) => Refresh(); followupFilter.IsCheckedChanged += (_, _) => Refresh(); statusFilter.SelectionChanged += (_, _) => Refresh();
        Opened += async (_, _) => { LoadWorkspace(); collectionTimer.Start(); await CollectAutomatically(); };
        // Closing would dispose the store under running background file work.
        Closing += (_, e) => { if (busy) { e.Cancel = true; SetStatus("Bitte warten: Die Verteilung läuft noch. Das Fenster lässt sich danach schließen.", FeedbackKind.Warning); } };
        Closed += (_, _) => { collectionTimer.Stop(); store?.Dispose(); store = null; session = null; schoolLogo?.Dispose(); schoolLogo = null; };
    }
    private void LoadWorkspace()
    {
        try
        {
            var root = explicitWorkspaceRoot ?? new TeacherWorkspaceLocation(TeacherWorkspace.DefaultRoot).Load();
            workspace = new TeacherWorkspace(root);
            if (workspace.Active is null && transferPackageDirectory is not null)
            {
                if (PedagogicalTransferPackage.FindSingleTransferFile(transferPackageDirectory) is { } transferFile)
                {
                    ShowTransferPreview(transferFile, workspace.Root);
                    return;
                }
                OpenActiveYear();
                SetStatus("Im Paketordner „Transfer“ wurde keine eindeutige .gsm-transfer-Datei gefunden. Bitte Transportdatei manuell öffnen.", FeedbackKind.Warning);
                return;
            }
            OpenActiveYear();
        }
        catch (Exception ex) { ShowStartupError(ex); }
    }
    private void ShowStartupError(Exception ex)
    {
        store?.Dispose(); store = null; session = null;
        workflowProgress.Children.Clear();
        heading.Text = "Verwaltungsstand konnte nicht geöffnet werden";
        navigation.Children.Clear();
        var panel = new StackPanel { Spacing = 14 };
        Intro(panel, "Gespeicherte Daten bleiben erhalten", ex.Message);
        panel.Children.Add(Text("Prüfen Sie Speicherort und Zugriffsrechte. Ein fehlender oder beschädigter Bestand wird nicht automatisch neu angelegt."));
        if (transferPackageDirectory is not null)
            panel.Children.Add(Text("Hinweis für das pädagogische Netz: Entpacken Sie das ZIP-Paket vollständig in einen lokalen Ordner (nicht direkt im ZIP und nicht auf einem Netzlaufwerk als Arbeitsstand öffnen)."));
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        var retry = Primary(Button("Erneut versuchen", LoadWorkspace)); retry.Margin = new Thickness(0, 0, 8, 6);
        actions.Children.Add(retry);
        actions.Children.Add(Button("Speicherort und Einstellungen", ShowSettings));
        panel.Children.Add(actions);
        pageHost.Content = Scroll(panel); SetStatus("Keine Änderungen am gespeicherten Bestand.", FeedbackKind.Warning);
    }
    // School presets beside the teacher program; a broken file only disables the presets and is reported.
    // A test stage brings its own file, so it can never preselect the real school drive.
    private SchoolDefaults SchoolPresets()
    {
        var programDirectory = transferPackageDirectory is null
            ? AppContext.BaseDirectory
            : Path.Combine(transferPackageDirectory, PedagogicalTransferPackage.TeacherFolderName);
        var directory = explicitWorkspaceRoot is not null && File.Exists(Path.Combine(explicitWorkspaceRoot, SchoolDefaults.FileName))
            ? explicitWorkspaceRoot
            : programDirectory;
        try { return SchoolDefaults.Load(directory); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        { SetStatus(SchoolDefaults.FileName + " nicht verwendet: " + ex.Message, FeedbackKind.Warning); return SchoolDefaults.None; }
    }
    private void OpenActiveYear()
    {
        store?.Dispose(); store = null; session = null;
        activeAsvComparison = null; distributionRoot.Text = ""; deliveryPreview.Children.Clear();
        collections = []; conflictSelection.Clear(); dismissedConflicts.Clear(); conflict.IsChecked = false; collectionResults.Children.Clear(); detail.Children.Clear();
        if (conflictSection is not null) conflictSection.IsVisible = false;
        importRows = []; importOrigin = null; table = null; importFormatNotice = null; mapping.Clear(); importResults.Children.Clear();
        var year = workspace?.Active;
        if (year is null) { ShowNewYear(); return; }
        try { store = workspace!.OpenActive(); }
        catch (Exception ex) { ShowStartupError(ex); return; }
        // A committed planning decision and all corresponding revisions live in one SQLite transaction.
        // The workspace catalog is only synchronized after that transaction, including after a crash.
        if (store is IPlanningStore planning && planning.ActivePlanningCatalog() is { } authoritative
            && System.Text.Json.JsonSerializer.Serialize(year.Catalog, DocumentJson.Options) != System.Text.Json.JsonSerializer.Serialize(authoritative, DocumentJson.Options))
        {
            year = year with { Catalog = authoritative };
            workspace!.Update(year);
        }
        actor.Text = year.Actor;
        session = new TeacherSession(store, year.Catalog, year.Actor);
        heading.Text = "Abitur " + year.Catalog.GraduationYear;
        SetStatus("Aktueller Stand geladen. Bestätigte Änderungen werden automatisch gespeichert.", FeedbackKind.Info);
        var roots = CurrentStudents().Where(s => s.MappingConfirmed && Path.IsPathFullyQualified(s.HomeDirectory))
            .Select(s => Path.GetDirectoryName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(s.HomeDirectory))))
            .Where(p => p is not null).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        distributionRoot.Text = roots.Length == 1 ? roots[0] : SchoolPresets().DistributionRoot;
        Refresh(); RebuildNavigation(); ShowCurrentStep();
    }
    private void RebuildNavigation()
    {
        navigation.Children.Clear();
        if (workspace?.Active?.Step == YearSetupStep.Complete)
        {
            navigation.Children.Add(Button("Übersicht", () => ShowPage("start"), "Startseite mit dem nächsten empfohlenen Arbeitsschritt und den wichtigsten Zahlen des Jahrgangs."));
            navigation.Children.Add(Button("Wahl", ShowElection, "Wahl vorbereiten und durchführen: Fächer, Schülerliste, Schülerbestand, Schülerdateien verteilen, Kurswahlen einsammeln."));
            navigation.Children.Add(Button("Kurswahlen", ShowResults, "Alle Kurswahlen filtern, einzeln ansehen und ändern, Abgänger markieren, Kontrollbögen, Kurslisten und Nachwahlbögen drucken."));
            navigation.Children.Add(Button("Kursplanung", ShowPlanning, "Wahlzahlen je Fach und Halbjahr, Planungsstände sichern, Kursstreichungen simulieren und Nachwahlbögen erstellen."));
            navigation.Children.Add(Button("ASV-Abgleich", ShowReconciliation, "ASV-Export „zz Kursstufe Fächerwahl“ einlesen und Deckungsgleichheit prüfen."));
        }
        else
            navigation.Children.Add(Button("Zur Einrichtung", () => { ClearError(); if (store is null) LoadWorkspace(); else ShowCurrentStep(); }, "Zurück zum geführten Einrichtungsassistenten."));
        navigation.Children.Add(Button("Einstellungen", ShowSettings, "Speicherort, Jahrgang wechseln oder neu anlegen, Bearbeiterkennung, Transportdatei öffnen."));
        navigation.Children.Add(Button("Hilfe", () => { ClearError(); visiblePage = "help"; pageHost.Content = Scroll(BuildOrientation()); UpdateVisualNavigation(); }, "Jahresablauf, Fachbegriffe und Dateitypen erklärt."));
        UpdateVisualNavigation();
    }
    private void ShowNewYear()
    {
        ClearError();
        visiblePage = "new"; heading.Text = "Willkommen in der Lehrerverwaltung"; RebuildNavigation();
        var panel = new StackPanel { Spacing = 14, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        Intro(panel, "Abiturjahrgang einrichten", "Sie legen den Jahrgang einmal an. Danach öffnet das Programm automatisch den zuletzt aktiven Stand und zeigt die nächsten Aufgaben.");
        var mainCard = new StackPanel { Spacing = 10 };
        mainCard.Children.Add(Heading("Neuen Abiturjahrgang starten"));
        var year = new NumericUpDown { Name = "SetupYear", Value = 2029, Minimum = 2000, Maximum = 2200, FormatString = "0", Width = 180 };
        mainCard.Children.Add(Labeled("Abiturjahrgang", year, "Vierstellige Jahreszahl des Abschlussjahrgangs (z. B. 2029)."));
        var editor = new TextBox { Name = "SetupActor", Text = "demo-beratung", Width = 300 };
        mainCard.Children.Add(Labeled("Bearbeiterkennung", editor, "Name/Kürzel für das Änderungsprotokoll (im Erprobungsbetrieb z. B. demo-beratung)."));
        mainCard.Children.Add(new TextBlock { Text = "Das vorbereitete GSM-Fachangebot wird als Ausgangspunkt geladen. Sie wählen zuerst die Leistungsfächer und laden anschließend die Schülerliste.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        var create = Primary(Button("Jahrgang anlegen und Fächer auswählen", () =>
        {
            if (workspace is null) throw new InvalidOperationException("Speicherort zuerst prüfen.");
            var basis = JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json")));
            var graduation = (int)(year.Value ?? 0);
            var version = graduation == basis.GraduationYear ? basis.Version : $"demo-{graduation}-" + Guid.NewGuid().ToString("N")[..8];
            workspace.Create(basis with { GraduationYear = graduation, Version = version }, editor.Text?.Trim() ?? "");
            OpenActiveYear();
        }));
        create.Name = "CreateYear"; create.Margin = new Thickness(0, 0, 8, 6);
        actions.Children.Add(create);
        if (workspace?.Active is not null) actions.Children.Add(Button("Zum bisherigen Jahrgang zurück", OpenActiveYear));
        mainCard.Children.Add(actions);
        panel.Children.Add(Surface(mainCard, 16));

        var transferCard = new StackPanel { Spacing = 8 };
        transferCard.Children.Add(Heading("Bestehenden Stand aus anderem Netz übernehmen"));
        transferCard.Children.Add(new TextBlock { Text = "Wenn ein Jahrgang bereits im Verwaltungs- oder pädagogischen Netz vorbereitet wurde, können Sie dessen Transportdatei hier direkt öffnen.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
        transferCard.Children.Add(Button("Transportdatei öffnen …", ImportTransfer));
        transferCard.Children.Add(Help("Vorhandene Daten oder anderer Speicherort?", "Unter Einstellungen können Sie einen bestehenden Lehrerbestand einmalig einbinden oder ein anderes lokales Verwaltungsverzeichnis wählen, etwa neben dem Programm. Ohne Auswahl nutzt die App ihren lokalen Datenordner. Bereits bestehende Dateien werden nicht verschoben."));
        panel.Children.Add(SubtleCard(transferCard, 14));

        pageHost.Content = Scroll(panel); SetStatus("Einrichtung wird nach jedem bestätigten Schritt gespeichert.", FeedbackKind.Info);
        UpdateWorkflowProgress(); UpdateVisualNavigation();
    }
    private void ShowCurrentStep()
    {
        var active = workspace?.Active; if (active is null) { ShowNewYear(); return; }
        UpdateWorkflowProgress();
        switch (active.Step)
        {
            case YearSetupStep.Complete: ShowPage("start"); break;
            case YearSetupStep.Offer: ShowOfferSetup(); break;
            case YearSetupStep.Import: ShowPage("import"); break;
            default: ShowPage("profiles"); break;
        }
    }
    public void JumpToWorkflowStep(int index)
    {
        var active = workspace?.Active;
        if (active is null || store is null || session is null) return;
        ClearError();
        try
        {
            if (index == 0)
            {
                if (session.Overview().Any(s => s.Record.Student.GraduationYear == session.Catalog.GraduationYear && s.Active is not null))
                { BuildCatalog(); visiblePage = "catalog"; pageHost.Content = catalogView; }
                else if (active.Step == YearSetupStep.Complete)
                    ShowOfferSetup();
                else
                    SetStep(YearSetupStep.Offer);
            }
            else if (index == 1)
            {
                if (active.Catalog.Offers.Count(o => o.Type == CourseType.Lf && o.Status is OfferStatus.Offered or OfferStatus.Established) < 3)
                    throw new InvalidOperationException("Bitte mindestens drei Leistungsfachangebote auswählen.");
                if (active.Step == YearSetupStep.Complete)
                    ShowPage("import");
                else
                    SetStep(YearSetupStep.Import);
            }
            else if (index == 2)
            {
                if (!CurrentStudents().Any())
                    throw new InvalidOperationException("Zuerst die Schülerliste übernehmen.");
                if (active.Step == YearSetupStep.Complete)
                    ShowPage("profiles");
                else
                    SetStep(YearSetupStep.Mapping);
            }
            else if (index == 3) ShowPage("collection");
            UpdateDashboard(); UpdateVisualNavigation(); UpdateWorkflowProgress();
        }
        catch (Exception ex) { SetError(ex.Message); }
    }
    private StudentRecord[] CurrentStudents() => store!.Students().Where(s => s.Student.GraduationYear == workspace!.Active!.Catalog.GraduationYear).ToArray();
    private void SetStep(YearSetupStep step)
    {
        if (step == YearSetupStep.Offer && session!.Overview().Any(s => s.Record.Student.GraduationYear == session.Catalog.GraduationYear && s.Active is not null))
            throw new InvalidOperationException("Für diesen Jahrgang liegen bereits Kurswahlen vor. Das aktive Fachangebot bleibt erhalten; eine spätere Angebotsänderung benötigt einen gesonderten Änderungsablauf.");
        workspace!.Update(workspace.Active! with { Step = step }); RebuildNavigation(); ShowCurrentStep();
    }
    private void ShowOfferSetup()
    {
        ClearError();
        visiblePage = "offer";
        var panel = new StackPanel { Spacing = 12 };
        Intro(panel, "1 · Fächer auswählen", "Wählen Sie die Leistungsfächer für diesen Abiturjahrgang. Ihre Auswahl wird sofort gespeichert. Basis- und Wahlfächer stammen aus dem vorbereiteten GSM-Angebot.");
        var active = workspace!.Active!;
        var lfCounter = new TextBlock { Name = "OfferLfCounter", FontSize = 14, FontWeight = FontWeight.SemiBold };
        var offerErrorBox = InlineErrorBox("OfferSetupError", out var offerErrorText);
        void UpdateLfCounter(OfferCatalog catalog)
        {
            var count = catalog.Offers.Count(o => o.Type == CourseType.Lf && o.Status is OfferStatus.Offered or OfferStatus.Established);
            lfCounter.Text = $"{count} Leistungsfächer ausgewählt (mindestens 3 erforderlich)";
            lfCounter.Foreground = count < 3 ? Brush.Parse("#A32D2D") : Accent;
            if (count >= 3) SetInlineError(offerErrorBox, offerErrorText, null);
        }
        UpdateLfCounter(active.Catalog);
        panel.Children.Add(lfCounter);
        var subjects = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4) };
        panel.Children.Add(subjects);
        foreach (var offer in active.Catalog.Offers.Where(o => o.Type == CourseType.Lf))
        {
            var subject = active.Catalog.Subjects.Single(s => s.Id == offer.SubjectId);
            var box = new CheckBox { Name = "SetupOffer_" + offer.Id.Replace('-', '_'), Content = subject.Name + (offer.Variant == CourseVariant.Bilingual ? " · bilingual" : ""), IsChecked = offer.Status is OfferStatus.Offered or OfferStatus.Established };
            box.HorizontalAlignment = HorizontalAlignment.Stretch;
            var card = Surface(box, 12); card.Width = 260; card.Margin = new Thickness(0, 0, 12, 12);
            void PaintCard() { card.Background = box.IsChecked == true ? SoftAccent : Brushes.White; card.BorderBrush = box.IsChecked == true ? Accent : Line; }
            PaintCard();
            box.IsCheckedChanged += (_, _) =>
            {
                try
                {
                    var current = workspace.Active!;
                    if (session!.Overview().Any(s => s.Record.Student.GraduationYear == current.Catalog.GraduationYear && s.Active is not null))
                        throw new InvalidOperationException("Inzwischen liegen Kurswahlen vor. Änderungen nur über die Kursplanung mit Vorschau vornehmen.");
                    var enabled = current.Catalog.Offers.Where(o => o.Type == CourseType.Lf && o.Status is OfferStatus.Offered or OfferStatus.Established).Select(o => o.Id).ToHashSet();
                    if (box.IsChecked == true) enabled.Add(offer.Id); else enabled.Remove(offer.Id);
                    var prepared = CatalogConfiguration.Prepare(current.Catalog, current.Catalog.GraduationYear, "demo-" + current.Catalog.GraduationYear + "-" + Guid.NewGuid().ToString("N")[..8], enabled);
                    workspace.Update(current with { Catalog = prepared });
                    session = new TeacherSession(store!, prepared, current.Actor);
                    PaintCard(); UpdateLfCounter(prepared); SetStatus("Fachauswahl gespeichert.", FeedbackKind.Success);
                }
                catch (Exception ex) { SetStatus("Fachauswahl nicht gespeichert: " + ex.Message, FeedbackKind.Error); ShowOfferSetup(); }
            };
            subjects.Children.Add(card);
        }
        var optional = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8) };
        foreach (var offer in active.Catalog.Offers.Where(o => o.Type != CourseType.Lf && PlanningPresentation.CanCancel(o)))
        {
            var subject = active.Catalog.Subjects.Single(s => s.Id == offer.SubjectId);
            var box = new CheckBox
            {
                Name = "SetupOffer_" + offer.Id.Replace('-', '_'),
                Content = subject.Name + " · " + FormatCourseType(offer.Type) + (offer.Variant == CourseVariant.Bilingual ? " bilingual" : ""),
                IsChecked = offer.Status is OfferStatus.Offered or OfferStatus.Established
            };
            box.HorizontalAlignment = HorizontalAlignment.Stretch;
            var card = Surface(box, 12); card.Width = 260; card.Margin = new Thickness(0, 0, 12, 12);
            void PaintCard() { card.Background = box.IsChecked == true ? SoftAccent : Brushes.White; card.BorderBrush = box.IsChecked == true ? Accent : Line; }
            PaintCard();
            box.IsCheckedChanged += (_, _) =>
            {
                try
                {
                    var current = workspace.Active!;
                    if (session!.Overview().Any(s => s.Record.Student.GraduationYear == current.Catalog.GraduationYear && s.Active is not null))
                        throw new InvalidOperationException("Inzwischen liegen Kurswahlen vor. Änderungen nur über die Kursplanung mit Vorschau vornehmen.");
                    var enabledLf = current.Catalog.Offers.Where(o => o.Type == CourseType.Lf && o.Status is OfferStatus.Offered or OfferStatus.Established).Select(o => o.Id).ToHashSet();
                    var enabledOptional = current.Catalog.Offers.Where(o => o.Type != CourseType.Lf && PlanningPresentation.CanCancel(o) && o.Status is OfferStatus.Offered or OfferStatus.Established).Select(o => o.Id).ToHashSet();
                    if (box.IsChecked == true) enabledOptional.Add(offer.Id); else enabledOptional.Remove(offer.Id);
                    var prepared = CatalogConfiguration.Prepare(current.Catalog, current.Catalog.GraduationYear, "demo-" + current.Catalog.GraduationYear + "-" + Guid.NewGuid().ToString("N")[..8], enabledLf, enabledOptional);
                    workspace.Update(current with { Catalog = prepared });
                    session = new TeacherSession(store!, prepared, current.Actor);
                    PaintCard(); SetStatus("Fachauswahl gespeichert.", FeedbackKind.Success);
                }
                catch (Exception ex) { SetStatus("Fachauswahl nicht gespeichert: " + ex.Message, FeedbackKind.Error); ShowOfferSetup(); }
            };
            optional.Children.Add(card);
        }
        panel.Children.Add(new Expander { Header = "Optionale Basis-, Wahl- und Seminarkurse anpassen (z. B. Italienisch, Psychologie, SIA)", Content = optional });
        panel.Children.Add(CourseAnnotationEditor());
        var deadline = new CalendarDatePicker { Name = "SetupDeadline", Width = 220, SelectedDate = active.SubmissionDeadline?.ToDateTime(TimeOnly.MinValue) };
        deadline.SelectedDateChanged += (_, _) =>
        {
            try
            {
                var value = deadline.SelectedDate is { } date ? DateOnly.FromDateTime(date) : (DateOnly?)null;
                if (value == workspace.Active!.SubmissionDeadline) return;
                workspace.Update(workspace.Active! with { SubmissionDeadline = value });
                SetStatus(value is null ? "Abgabetermin entfernt." : $"Abgabetermin {value:dd.MM.yyyy} gespeichert; er erscheint nach der nächsten Verteilung im Schülerprogramm.", FeedbackKind.Success);
            }
            catch (Exception ex) { SetStatus("Abgabetermin nicht gespeichert: " + ex.Message, FeedbackKind.Error); }
        };
        panel.Children.Add(SubtleCard(Labeled("Abgabetermin (nur Hinweis)", deadline, "Wird den Schülern im Programm angezeigt. Das Programm sperrt danach nichts; Sie sammeln nach dem Termin unter „Kurswahlen einsammeln“ abschließend ein.")));
        var program = new TextBox { Name = "SetupStudentProgram", Text = active.StudentProgramDirectory.Length > 0 ? active.StudentProgramDirectory : SchoolPresets().StudentProgramDirectory, Watermark = "Ordner mit dem veröffentlichten Schülerprogramm (optional)", Width = 520 };
        static string ProgramHeader(string path) => string.IsNullOrWhiteSpace(path)
            ? "Erweitert: Schülerprogramm automatisch mitverteilen (nicht konfiguriert)"
            : $"Erweitert: Schülerprogramm automatisch mitverteilen ({path})";
        var programExpander = new Expander { Name = "SetupStudentProgramExpander", Header = ProgramHeader(program.Text ?? ""), IsExpanded = false, HorizontalAlignment = HorizontalAlignment.Stretch };
        var saveProgram = Button("Programmordner übernehmen", () =>
        {
            var value = (program.Text ?? "").Trim();
            if (value.Length > 0) StudentProgramPackage.Validate(value);
            var normalized = value.Length == 0 ? "" : Path.GetFullPath(value);
            workspace.Update(workspace.Active! with { StudentProgramDirectory = normalized });
            programExpander.Header = ProgramHeader(normalized);
            SetStatus(value.Length == 0 ? "Schülerprogramm wird nicht mitverteilt." : "Schülerprogramm wird bei der Verteilung in jeden Wahlordner kopiert.", FeedbackKind.Success);
        }, "Leer lassen, wenn das Schülerprogramm anders bereitgestellt wird.");
        var programRow = new WrapPanel { Orientation = Orientation.Horizontal };
        program.Margin = new Thickness(0, 0, 8, 6);
        programRow.Children.Add(program); programRow.Children.Add(saveProgram);
        programExpander.Content = Labeled("Schülerprogramm mitverteilen", programRow, "Optional. Das Programm wird nach „" + StudentFolderCleanup.ProgramFolder + "“ im persönlichen Ordner kopiert, daneben liegt die Startdatei „" + StudentFolderLayout.LauncherFile + "“. Beim abschließenden Einsammeln wird beides wieder entfernt. Die Startdatei übergibt den Datenordner; appsettings.json muss nicht angepasst werden.");
        panel.Children.Add(programExpander);
        panel.Children.Add(offerErrorBox);
        var next = Button("Fachangebot bestätigen · weiter zur Schülerliste", () =>
        {
            if (workspace.Active!.Catalog.Offers.Count(o => o.Type == CourseType.Lf && o.Status is OfferStatus.Offered or OfferStatus.Established) < 3)
            {
                SetInlineError(offerErrorBox, offerErrorText, "Bitte mindestens drei Leistungsfachangebote auswählen.");
                throw new InvalidOperationException("Bitte mindestens drei Leistungsfachangebote auswählen.");
            }
            if (workspace.Active.Step == YearSetupStep.Complete && CurrentStudents().Any())
                ShowPage("start");
            else
                SetStep(YearSetupStep.Import);
        }); next.Name = "ConfirmSetupOffer"; panel.Children.Add(Primary(next));
        pageHost.Content = Scroll(panel); UpdateVisualNavigation();
    }
    private void ShowPage(string page)
    {
        if (page == "results") { ShowResults(); return; }
        ClearError();
        visiblePage = page;
        if (page == "start") UpdateDashboard();
        if (page is "people" or "profiles") Refresh();
        UpdateVisualNavigation();
        pageHost.Content = page switch { "people" => peopleView, "import" => importView, "profiles" => profileView, "collection" => collectionView, _ => dashboardView };
    }
    private void UpdateDashboard()
    {
        if (session is null || store is null || workspace?.Active?.Step != YearSetupStep.Complete) return;
        var summary = TeacherDashboardBuilder.Build(store, session);
        dashboardPanel.Children.Clear(); UpdateWorkflowProgress();
        dashboardPanel.Children.Add(new TextBlock { Text = "Ihr aktueller Jahrgang", FontSize = 23, FontWeight = FontWeight.SemiBold, Foreground = Ink });
        string next; string action; string target;
        if (collections.Length > 0) { next = $"{collections.Length} Wahlkonflikte benötigen eine Entscheidung."; action = "Konflikte prüfen"; target = "collection"; }
        else if (summary.Students > 0 && summary.Choices == summary.Students)
        {
            next = summary.Reselections > 0 || summary.Reviews > 0
                ? $"Alle {summary.Choices} Kurswahlen liegen vor ({summary.Reselections} offene Nachwahlen, {summary.Reviews} zu klären). Prüfen Sie die Ergebnisse und planen Sie das Angebot."
                : $"Alle {summary.Choices} Kurswahlen liegen vollständig vor. Prüfen Sie die Ergebnisse und planen Sie das verbleibende Angebot.";
            action = "Kurswahlen prüfen und ändern"; target = "results";
        }
        else if (summary.Students == 0) { next = "Für diesen Jahrgang fehlen noch Schüler."; action = "Schülerliste importieren"; target = "import"; }
        else if (summary.UnconfirmedMappings > 0) { next = $"{summary.UnconfirmedMappings} Ordnerzuordnungen müssen im pädagogischen Netz geprüft und verteilt werden."; action = "Schülerdateien verteilen"; target = "profiles"; }
        else if (summary.ProfilesToIssue > 0) { next = $"Für {summary.ProfilesToIssue} Personen fehlen aktuelle Profile (Verteilung erfolgt im pädagogischen Netz)."; action = "Schülerprofile bereitstellen"; target = "profiles"; }
        else if (summary.ProfilesToDeliver > 0) { next = $"{summary.ProfilesToDeliver} Profile sind noch im pädagogischen Netz zuzustellen oder erneut zu verteilen."; action = "Zustellung bearbeiten"; target = "profiles"; }
        else if (summary.Choices < summary.Students) { next = $"Für {summary.Students - summary.Choices} Personen wurde noch keine Kurswahl im pädagogischen Netz eingelesen."; action = "Kurswahlen einsammeln"; target = "collection"; }
        else { next = "Die Kurswahlen liegen vor. Prüfen Sie die Ergebnisse und planen Sie das verbleibende Angebot."; action = "Kurswahlen prüfen und ändern"; target = "results"; }
        var primary = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var guidance = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
        guidance.Children.Add(new TextBlock { Text = "Als Nächstes", FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Accent });
        guidance.Children.Add(Text(next)); primary.Children.Add(guidance);
        var nextButton = Primary(Button(action, () => ShowPage(target))); nextButton.Name = "NextTask"; nextButton.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(nextButton, 1); primary.Children.Add(nextButton);
        var nextCard = Surface(primary, 18); nextCard.Background = HighlightAccent; nextCard.BorderBrush = Accent; dashboardPanel.Children.Add(nextCard);
        dashboardPanel.Children.Add(SummaryCards(summary));
        dashboardPanel.Children.Add(new TextBlock { Text = $"{summary.Reselections} Nachwahlen · Neue Kurswahlen werden bei geöffneter App alle 60 Sekunden eingelesen. Kacheln oben anklicken zum Öffnen.", Foreground = Muted, TextWrapping = TextWrapping.Wrap });
        dashboardPanel.Children.Add(new TextBlock { Text = "Weitere Aufgaben", FontWeight = FontWeight.SemiBold });
        var tasks = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var task in new[] {
            Button("Wahl organisieren", ShowElection, "Schülerliste, Schülerdateien und Wahleingang verwalten."),
            Button("Schülerbestand und Sonderfälle", () => ShowPage("people"), "Einzelne Personen öffnen: Vorbildung, Nachwahl, Lehreränderung und Historie."),
            Button("Kurswahlen prüfen und ändern", ShowResults, "Alle Wahlen filtern, einzeln ändern und drucken."),
            Button("Kursplanung öffnen", ShowPlanning, "Wahlzahlen auswerten und Kursstreichungen simulieren."),
            Button("ASV-Abgleich öffnen", ShowReconciliation, "ASV-Export „zz Kursstufe Fächerwahl“ auf Deckungsgleichheit prüfen.") })
        { task.Margin = new Thickness(0, 0, 10, 10); tasks.Children.Add(task); }
        dashboardPanel.Children.Add(tasks);
    }
    private void ShowManage()
    {
        ClearError();
        visiblePage = "manage"; managePanel.Children.Clear();
        Intro(managePanel, "Jahrgang verwalten", "Der aktive Jahrgang wird beim nächsten Start automatisch geöffnet. Ein Kalenderwechsel ändert ihn nicht.");
        var yearsStack = new StackPanel { Spacing = 8 };
        yearsStack.Children.Add(Heading("Eingerichtete Abiturjahrgänge"));
        foreach (var year in workspace!.Document.Years.OrderBy(y => y.Catalog.GraduationYear))
        {
            var isCurrent = year.Catalog.GraduationYear == workspace.Active?.Catalog.GraduationYear;
            var btn = Button($"Abitur {year.Catalog.GraduationYear} · {(year.Step == YearSetupStep.Complete ? "eingerichtet" : "Einrichtung fortsetzen")}", () =>
            { workspace.Activate(year.Catalog.GraduationYear); OpenActiveYear(); });
            if (isCurrent)
            {
                var row = new WrapPanel { Orientation = Orientation.Horizontal };
                btn.Margin = new Thickness(0, 0, 10, 0);
                row.Children.Add(btn);
                row.Children.Add(new Border
                {
                    Background = SoftAccent, CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 4), VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = "aktuell geöffnet", FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Accent }
                });
                var card = SubtleCard(row, 10); card.BorderBrush = Accent;
                yearsStack.Children.Add(card);
            }
            else
                yearsStack.Children.Add(btn);
        }
        managePanel.Children.Add(Surface(yearsStack, 14));
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        void AddManageAction(Button b) { b.Margin = new Thickness(0, 0, 8, 6); actions.Children.Add(b); }
        AddManageAction(Button("Weiteren Abiturjahrgang einrichten", ShowNewYear));
        AddManageAction(Button("Fachangebot ansehen / für Schülerprogramm exportieren", () => { ClearError(); BuildCatalog(); visiblePage = "catalog"; pageHost.Content = catalogView; UpdateVisualNavigation(); }));
        if (session is not null && !session.Overview().Any(s => s.Record.Student.GraduationYear == session.Catalog.GraduationYear && s.Active is not null))
            AddManageAction(Button("Fachauswahl erneut bearbeiten", () => { ShowOfferSetup(); UpdateVisualNavigation(); UpdateWorkflowProgress(); }, "Öffnet die Fachauswahl erneut. Schülerdaten bleiben erhalten. Bei bereits eingelesenen Wahlen ist diese Änderung gesperrt."));
        managePanel.Children.Add(actions);
        pageHost.Content = manageView; UpdateVisualNavigation();
    }
    private void ShowSettings()
    {
        ClearError();
        RebuildNavigation();
        visiblePage = "settings"; settingsPanel.Children.Clear();
        Intro(settingsPanel, "Einstellungen", "Speicherort und technische Einrichtung werden hier einmalig festgelegt. Im Alltag öffnet sich Ihr gespeicherter Jahrgang automatisch.");
        var mainStack = new StackPanel { Spacing = 10 };
        mainStack.Children.Add(Heading("Jahrgang & Bearbeiter"));
        if (workspace?.Active is not null) mainStack.Children.Add(Button("Jahrgang verwalten", ShowManage));
        if (workspace?.Active is { } active)
        {
            var editor = new TextBox { Text = active.Actor, Width = 280 };
            var actorRow = new WrapPanel { Orientation = Orientation.Horizontal };
            editor.Margin = new Thickness(0, 0, 8, 6);
            actorRow.Children.Add(editor);
            actorRow.Children.Add(Button("Bearbeiter speichern", () => { workspace.Update(active with { Actor = editor.Text?.Trim() ?? "" }); OpenActiveYear(); }));
            mainStack.Children.Add(Labeled("Bearbeiterkennung", actorRow, "Name oder Kürzel für alle neu gespeicherten Änderungen im Änderungsprotokoll."));
        }
        settingsPanel.Children.Add(Surface(mainStack, 14));

        var transferStack = new StackPanel { Spacing = 8 };
        transferStack.Children.Add(Heading("Netzwechsel & Sicherung"));
        transferStack.Children.Add(new TextBlock { Text = "Überträgt den kompletten Jahrgang sicher zwischen Verwaltungs- und pädagogischem Netz oder erstellt ein komplettes Paket inkl. Programm für das pädagogische Netz.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
        var transferButtons = new WrapPanel { Orientation = Orientation.Horizontal };
        if (store is SqliteAdministrationStore && session is not null)
        {
            var pkgBtn = Primary(Button("Paket für pädagogisches Netz speichern (.zip) …", ExportPedagogicalPackage));
            pkgBtn.Margin = new Thickness(0, 0, 8, 6);
            transferButtons.Children.Add(pkgBtn);
            var saveBtn = Button("Jahrgang sichern …", () => ExportTransfer());
            saveBtn.Margin = new Thickness(0, 0, 8, 6);
            transferButtons.Children.Add(saveBtn);
        }
        var openTransfer = Button("Transportdatei öffnen …", ImportTransfer); openTransfer.Margin = new Thickness(0, 0, 0, 6);
        transferButtons.Children.Add(openTransfer);
        transferStack.Children.Add(transferButtons);
        settingsPanel.Children.Add(SubtleCard(transferStack, 14));

        var storageStack = new StackPanel { Spacing = 8 };
        storageStack.Children.Add(Text("Verwaltungsverzeichnis: " + (workspace?.Root ?? explicitWorkspaceRoot ?? TeacherWorkspace.DefaultRoot)));
        storageStack.Children.Add(new TextBlock { Text = "Ein anderes Verzeichnis öffnet dessen Bestand oder beginnt dort eine neue Einrichtung. Vorhandene Daten werden nicht verschoben.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var storageButtons = new WrapPanel { Orientation = Orientation.Horizontal };
        var changeDir = Button("Anderes lokales Verwaltungsverzeichnis öffnen …", async () =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Lokales Verwaltungsverzeichnis wählen (auch neben dem Programm möglich)", AllowMultiple = false });
            var root = folders.FirstOrDefault()?.TryGetLocalPath(); if (root is null) return;
            var next = new TeacherWorkspace(root);
            // Check availability before making this location the default. Nothing is moved or copied.
            if (next.Active is not null && !string.Equals(next.Root, workspace?.Root, StringComparison.OrdinalIgnoreCase)) using (var check = next.OpenActive()) { }
            if (explicitWorkspaceRoot is null) new TeacherWorkspaceLocation(TeacherWorkspace.DefaultRoot).Remember(next.Root);
            workspace = next; OpenActiveYear();
        });
        changeDir.Margin = new Thickness(0, 0, 8, 6);
        storageButtons.Children.Add(changeDir);
        storageButtons.Children.Add(Button("Vorhandenen Lehrerbestand einmalig einbinden …", AttachExisting));
        storageStack.Children.Add(storageButtons);
        settingsPanel.Children.Add(new Expander
        {
            Name = "SettingsStorageExpander",
            Header = "Speicherort & bestehende Datenbank (Ersteinrichtung)",
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = storageStack
        });
        pageHost.Content = settingsView; UpdateVisualNavigation();
    }
    private async Task AttachExisting()
    {
        if (workspace is null) throw new InvalidOperationException("Zuerst ein gültiges Verwaltungsverzeichnis auswählen.");
        var catalogPath = await Pick("Fachangebot des bestehenden Bestands", "Fachangebot (.json)", "*.json"); if (catalogPath is null) return;
        var catalog = JsonDocuments.ReadCatalog(await File.ReadAllBytesAsync(catalogPath));
        var databasePath = await Pick("Bestehende lokale Verwaltungsdatei auswählen", "Lehrerbestand (.db)", "*.db"); if (databasePath is null) return;
        workspace.Attach(catalog, databasePath, workspace.Active?.Actor ?? "demo-beratung"); OpenActiveYear();
    }
    private async Task ExportCurrentCatalog()
    {
        Ready();
        var catalog = session!.Catalog;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        { Title = "Aktuelles Fachangebot für Schülerinstallation", SuggestedFileName = $"fachangebot-{catalog.GraduationYear}.json", DefaultExtension = "json", FileTypeChoices = [new FilePickerFileType("Fachangebot (.json)") { Patterns = ["*.json"] }] });
        var path = file?.TryGetLocalPath(); if (path is null) return;
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(catalog, DocumentJson.Options)); output.Flush(true); }
            File.Move(temp, path, overwrite: false);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        SetStatus("Aktuelles Fachangebot exportiert. Diese Datei als Katalog der Schülerinstallation konfigurieren; vorhandene Dateien bleiben erhalten.", FeedbackKind.Success);
    }
}
