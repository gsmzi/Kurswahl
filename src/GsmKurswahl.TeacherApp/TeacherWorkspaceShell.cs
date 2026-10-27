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
    private TeacherWorkspace? workspace;
    private readonly ContentControl pageHost = new();
    private readonly StackPanel navigation = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly StackPanel dashboardPanel = new() { Spacing = 16 };
    private readonly StackPanel managePanel = new() { Spacing = 12 };
    private readonly StackPanel settingsPanel = new() { Spacing = 12 };
    private readonly TextBlock heading = new() { FontSize = 28, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private Control peopleView = null!;
    private Control importView = null!;
    private Control profileView = null!;
    private Control collectionView = null!;
    private Control dashboardView = null!;
    private Control settingsView = null!, manageView = null!, catalogView = null!;
    private string visiblePage = "start";

    // Tests pass an isolated root; production remembers a local administrative directory.
    public TeacherWindow(string? workspaceRoot = null)
    {
        explicitWorkspaceRoot = workspaceRoot;
        Title = "GSM-Kurswahl | Lehrerverwaltung · M3"; Width = 1400; Height = 900; MinWidth = 900; MinHeight = 600;
        BuildVisualShell();
        var people = new DockPanel();
        var left = new StackPanel { Spacing = 8 }; left.Children.Add(filter); left.Children.Add(followupFilter); left.Children.Add(statusFilter); left.Children.Add(roster);
        personEditor.Content = Scroll(detail); DockPanel.SetDock(personEditor, Dock.Bottom); people.Children.Add(personEditor); people.Children.Add(Scroll(left));
        peopleView = people; importView = Scroll(importPanel); profileView = Scroll(profilePanel); collectionView = Scroll(collectionPanel); dashboardView = Scroll(dashboardPanel);
        settingsView = Scroll(settingsPanel); manageView = Scroll(managePanel); catalogView = Scroll(catalogPanel);
        BuildImport(); BuildProfiles(); BuildCollection();
        collectionTimer.Tick += async (_, _) => await CollectAutomatically();
        filter.TextChanged += (_, _) => Refresh(); followupFilter.IsCheckedChanged += (_, _) => Refresh(); statusFilter.SelectionChanged += (_, _) => Refresh();
        Opened += async (_, _) => { LoadWorkspace(); collectionTimer.Start(); await CollectAutomatically(); };
        Closed += (_, _) => { collectionTimer.Stop(); store?.Dispose(); store = null; session = null; };
    }
    private void LoadWorkspace()
    {
        try
        {
            var root = explicitWorkspaceRoot ?? new TeacherWorkspaceLocation(TeacherWorkspace.DefaultRoot).Load();
            workspace = new TeacherWorkspace(root);
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
        panel.Children.Add(Button("Erneut versuchen", LoadWorkspace));
        panel.Children.Add(Button("Speicherort und Einstellungen", ShowSettings));
        pageHost.Content = Scroll(panel); status.Text = "Keine Änderungen am gespeicherten Bestand.";
    }
    private void OpenActiveYear()
    {
        store?.Dispose(); store = null; session = null;
        distributionRoot.Text = ""; deliveryPreview.Children.Clear();
        collections = []; collectionResults.Children.Clear(); detail.Children.Clear();
        importRows = []; importOrigin = null; table = null; mapping.Clear(); importResults.Children.Clear();
        var year = workspace?.Active;
        if (year is null) { ShowNewYear(); return; }
        try { store = workspace!.OpenActive(); }
        catch (Exception ex) { ShowStartupError(ex); return; }
        actor.Text = year.Actor;
        session = new TeacherSession(store, year.Catalog, year.Actor);
        heading.Text = "Abitur " + year.Catalog.GraduationYear;
        status.Text = "Aktueller Stand geladen. Bestätigte Änderungen werden automatisch gespeichert.";
        var roots = CurrentStudents().Where(s => Path.IsPathFullyQualified(s.HomeDirectory))
            .Select(s => Path.GetDirectoryName(Path.GetDirectoryName(s.HomeDirectory))).Where(p => p is not null).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (roots.Length == 1) distributionRoot.Text = roots[0];
        Refresh(); RebuildNavigation(); ShowCurrentStep();
    }
    private void RebuildNavigation()
    {
        navigation.Children.Clear();
        if (workspace?.Active?.Step == YearSetupStep.Complete)
        {
            navigation.Children.Add(Button("Übersicht", () => ShowPage("start")));
            navigation.Children.Add(Button("Schülerbestand", () => ShowPage("people")));
            navigation.Children.Add(Button("Jahrgang verwalten", ShowManage));
        }
        else
            navigation.Children.Add(Button("Zur Einrichtung", () => { if (store is null) LoadWorkspace(); else ShowCurrentStep(); }));
        navigation.Children.Add(Button("Einstellungen", ShowSettings));
        navigation.Children.Add(Button("Hilfe", () => { visiblePage = "help"; pageHost.Content = Scroll(BuildOrientation()); }));
        UpdateVisualNavigation();
    }
    private void ShowNewYear()
    {
        visiblePage = "new"; heading.Text = "Willkommen in der Lehrerverwaltung"; RebuildNavigation();
        var panel = new StackPanel { Spacing = 16, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        Intro(panel, "Abiturjahrgang einrichten", "Sie legen den Jahrgang einmal an. Danach öffnet das Programm automatisch den zuletzt aktiven Stand und zeigt die nächsten Aufgaben.");
        panel.Children.Add(Text("Abiturjahrgang"));
        var year = new NumericUpDown { Name = "SetupYear", Value = 2029, Minimum = 2000, Maximum = 2200, FormatString = "0", Width = 180 };
        panel.Children.Add(year);
        panel.Children.Add(Text("Bearbeiterkennung für das Änderungsprotokoll (Erprobungsbetrieb)"));
        var editor = new TextBox { Name = "SetupActor", Text = "demo-beratung", Width = 300 }; panel.Children.Add(editor);
        panel.Children.Add(Text("Das vorbereitete GSM-Fachangebot wird als Ausgangspunkt geladen. Sie wählen zuerst die Leistungsfächer und laden anschließend die Schülerliste."));
        var create = Button("Jahrgang anlegen und Fächer auswählen", () =>
        {
            if (workspace is null) throw new InvalidOperationException("Speicherort zuerst prüfen.");
            var basis = JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json")));
            var graduation = (int)(year.Value ?? 0);
            var version = graduation == basis.GraduationYear ? basis.Version : $"demo-{graduation}-" + Guid.NewGuid().ToString("N")[..8];
            workspace.Create(basis with { GraduationYear = graduation, Version = version }, editor.Text?.Trim() ?? "");
            OpenActiveYear();
        }); create.Name = "CreateYear"; panel.Children.Add(Primary(create));
        if (workspace?.Active is not null) panel.Children.Add(Button("Zum bisherigen Jahrgang zurück", OpenActiveYear));
        panel.Children.Add(Help("Vorhandene Daten oder anderer Speicherort?", "Unter Einstellungen können Sie einen bestehenden Lehrerbestand einmalig einbinden oder ein anderes lokales Verwaltungsverzeichnis wählen, etwa neben dem Programm. Ohne Auswahl nutzt die App ihren lokalen Datenordner. Bereits bestehende Dateien werden nicht überschrieben."));
        pageHost.Content = Scroll(panel); status.Text = "Einrichtung wird nach jedem bestätigten Schritt gespeichert.";
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
            UpdateDashboard(); UpdateVisualNavigation(); UpdateWorkflowProgress();
        }
        catch (Exception ex) { status.Text = "Vorgang nicht abgeschlossen: " + ex.Message; }
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
        visiblePage = "offer";
        var panel = new StackPanel { Spacing = 12 };
        Intro(panel, "1 · Fächer auswählen", "Wählen Sie die Leistungsfächer für diesen Abiturjahrgang. Ihre Auswahl wird sofort gespeichert. Basis- und Wahlfächer stammen aus dem vorbereiteten GSM-Angebot.");
        var active = workspace!.Active!;
        var subjects = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8) };
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
                    var enabled = current.Catalog.Offers.Where(o => o.Type == CourseType.Lf && o.Status is OfferStatus.Offered or OfferStatus.Established).Select(o => o.Id).ToHashSet();
                    if (box.IsChecked == true) enabled.Add(offer.Id); else enabled.Remove(offer.Id);
                    var prepared = CatalogConfiguration.Prepare(current.Catalog, current.Catalog.GraduationYear, "demo-" + current.Catalog.GraduationYear + "-" + Guid.NewGuid().ToString("N")[..8], enabled);
                    workspace.Update(current with { Catalog = prepared });
                    session = new TeacherSession(store!, prepared, current.Actor);
                    PaintCard(); status.Text = "Fachauswahl gespeichert.";
                }
                catch (Exception ex) { status.Text = "Fachauswahl nicht gespeichert: " + ex.Message; ShowOfferSetup(); }
            };
            subjects.Children.Add(card);
        }
        var next = Button("Fachangebot bestätigen · weiter zur Schülerliste", () =>
        {
            if (workspace.Active!.Catalog.Offers.Count(o => o.Type == CourseType.Lf && o.Status is OfferStatus.Offered or OfferStatus.Established) < 3)
                throw new InvalidOperationException("Bitte mindestens drei Leistungsfachangebote auswählen.");
            if (workspace.Active.Step == YearSetupStep.Complete && CurrentStudents().Any())
                ShowPage("start");
            else
                SetStep(YearSetupStep.Import);
        }); next.Name = "ConfirmSetupOffer"; panel.Children.Add(Primary(next));
        pageHost.Content = Scroll(panel); UpdateVisualNavigation();
    }
    private void ShowPage(string page)
    {
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
        if (summary.Students == 0) { next = "Für diesen Jahrgang fehlen noch Schüler."; action = "Schülerliste importieren"; target = "import"; }
        else if (summary.UnconfirmedMappings > 0) { next = $"{summary.UnconfirmedMappings} Ordnerzuordnungen müssen noch geprüft werden."; action = "Schülerdateien verteilen"; target = "profiles"; }
        else if (summary.ProfilesToIssue > 0) { next = $"Für {summary.ProfilesToIssue} Personen fehlen aktuelle Profile."; action = "Schülerprofile bereitstellen"; target = "profiles"; }
        else if (summary.ProfilesToDeliver > 0) { next = $"{summary.ProfilesToDeliver} Profile sind noch zuzustellen oder erneut zu verteilen."; action = "Zustellung bearbeiten"; target = "profiles"; }
        else if (collections.Length > 0) { next = $"Für {collections.Length} Personen liegen offene Wahlkonflikte zur Entscheidung vor."; action = "Konflikte prüfen"; target = "collection"; }
        else if (summary.Choices < summary.Students) { next = $"Für {summary.Students - summary.Choices} Personen wurde noch keine Kurswahl eingelesen."; action = "Kurswahlen einsammeln"; target = "collection"; }
        else { next = "Alle Personen haben einen eingelesenen Wahlstand. Prüfen Sie offene Fälle oder sammeln Sie neue Änderungen ein."; action = "Schülerbestand prüfen"; target = "people"; }
        var primary = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var guidance = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
        guidance.Children.Add(new TextBlock { Text = "Als Nächstes", FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Accent });
        guidance.Children.Add(Text(next)); primary.Children.Add(guidance);
        var nextButton = Primary(Button(action, () => ShowPage(target))); nextButton.Name = "NextTask"; nextButton.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(nextButton, 1); primary.Children.Add(nextButton);
        var nextCard = Surface(primary, 18); nextCard.Background = Brush.Parse("#F0F8F6"); nextCard.BorderBrush = Accent; dashboardPanel.Children.Add(nextCard);
        dashboardPanel.Children.Add(SummaryCards(summary));
        dashboardPanel.Children.Add(new TextBlock { Text = $"{summary.Reselections} Nachwahlen · Neue Kurswahlen werden bei geöffneter App alle 60 Sekunden eingelesen.", Foreground = Muted, TextWrapping = TextWrapping.Wrap });
        dashboardPanel.Children.Add(new TextBlock { Text = "Weitere Aufgaben", FontWeight = FontWeight.SemiBold });
        var tasks = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var task in new[] {
            Button("Schülerprofile bereitstellen / Zustellung wiederholen", () => ShowPage("profiles")),
            Button("Kurswahlen einsammeln", () => ShowPage("collection")),
            Button("Schülerbestand ansehen und bearbeiten", () => ShowPage("people")),
            Button("Schülerliste aktualisieren", () => ShowPage("import"), "Erneuter Import mit Vorschau. Bestehende Änderungen werden nicht automatisch überschrieben.") })
        { task.Margin = new Thickness(0, 0, 10, 10); tasks.Children.Add(task); }
        dashboardPanel.Children.Add(tasks);
    }
    private void ShowManage()
    {
        visiblePage = "manage"; managePanel.Children.Clear();
        Intro(managePanel, "Jahrgang verwalten", "Der aktive Jahrgang wird beim nächsten Start automatisch geöffnet. Ein Kalenderwechsel ändert ihn nicht.");
        foreach (var year in workspace!.Document.Years.OrderBy(y => y.Catalog.GraduationYear))
            managePanel.Children.Add(Button($"Abitur {year.Catalog.GraduationYear} · {(year.Step == YearSetupStep.Complete ? "eingerichtet" : "Einrichtung fortsetzen")}", () =>
            { workspace.Activate(year.Catalog.GraduationYear); OpenActiveYear(); }));
        managePanel.Children.Add(Button("Weiteren Abiturjahrgang einrichten", ShowNewYear));
        managePanel.Children.Add(Button("Fachangebot ansehen / für Schülerprogramm exportieren", () => { BuildCatalog(); visiblePage = "catalog"; pageHost.Content = catalogView; }));
        if (session is not null && !session.Overview().Any(s => s.Record.Student.GraduationYear == session.Catalog.GraduationYear && s.Active is not null))
            managePanel.Children.Add(Button("Fachauswahl erneut bearbeiten", () => { ShowOfferSetup(); UpdateVisualNavigation(); UpdateWorkflowProgress(); }, "Öffnet die Fachauswahl erneut. Schülerdaten bleiben erhalten. Bei bereits eingelesenen Wahlen ist diese Änderung gesperrt."));
        pageHost.Content = manageView; UpdateVisualNavigation();
    }
    private void ShowSettings()
    {
        RebuildNavigation();
        visiblePage = "settings"; settingsPanel.Children.Clear();
        Intro(settingsPanel, "Einstellungen", "Speicherort und technische Einrichtung werden hier einmalig festgelegt. Im Alltag öffnet sich Ihr gespeicherter Jahrgang automatisch.");
        settingsPanel.Children.Add(Text("Verwaltungsverzeichnis: " + (workspace?.Root ?? explicitWorkspaceRoot ?? TeacherWorkspace.DefaultRoot)));
        settingsPanel.Children.Add(Button("Anderes lokales Verwaltungsverzeichnis öffnen …", async () =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Lokales Verwaltungsverzeichnis wählen (auch neben dem Programm möglich)", AllowMultiple = false });
            var root = folders.FirstOrDefault()?.TryGetLocalPath(); if (root is null) return;
            var next = new TeacherWorkspace(root);
            // Check availability before making this location the default. Nothing is moved or copied.
            if (next.Active is not null && !string.Equals(next.Root, workspace?.Root, StringComparison.OrdinalIgnoreCase)) using (var check = next.OpenActive()) { }
            if (explicitWorkspaceRoot is null) new TeacherWorkspaceLocation(TeacherWorkspace.DefaultRoot).Remember(next.Root);
            workspace = next; OpenActiveYear();
        }));
        settingsPanel.Children.Add(Text("Ein anderes Verzeichnis öffnet dessen Bestand oder beginnt dort eine neue Einrichtung. Vorhandene Daten werden nicht verschoben."));
        settingsPanel.Children.Add(Button("Vorhandenen Lehrerbestand einmalig einbinden …", AttachExisting));
        if (workspace?.Active is { } active)
        {
            settingsPanel.Children.Add(Text("Bearbeiterkennung"));
            var editor = new TextBox { Text = active.Actor }; settingsPanel.Children.Add(editor);
            settingsPanel.Children.Add(Button("Bearbeiter speichern", () => { workspace.Update(active with { Actor = editor.Text?.Trim() ?? "" }); OpenActiveYear(); }));
        }
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
        status.Text = "Aktuelles Fachangebot exportiert. Diese Datei als Katalog der Schülerinstallation konfigurieren; vorhandene Dateien bleiben erhalten.";
    }
}
