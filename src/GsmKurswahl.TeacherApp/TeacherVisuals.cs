using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Markup.Xaml.Styling;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.ImportExport;

namespace GsmKurswahl.TeacherApp;

public sealed partial class TeacherWindow
{
    private static readonly IBrush Ink = Brush.Parse("#1B2038");
    private static readonly IBrush Muted = Brush.Parse("#5A6075");
    private static readonly IBrush Accent = Brush.Parse("#1E287D");
    private static readonly IBrush SoftAccent = Brush.Parse("#EAEBF7");
    private static readonly IBrush HighlightAccent = Brush.Parse("#F4F5FB");
    private static readonly IBrush Line = Brush.Parse("#D5D7DF");
    private static readonly IBrush RowAlt = Brush.Parse("#F4F5F9");
    private static readonly IBrush SuccessInk = Brush.Parse("#2B6B2F");
    private static readonly IBrush SuccessBorder = Brush.Parse("#3B6D11");
    private static readonly IBrush SuccessSoft = Brush.Parse("#E8F4EA");
    private enum FeedbackKind { Info, Success, Warning, Error }
    private readonly StackPanel workflowProgress = new() { Name = "WorkflowProgress", Spacing = 10 };
    private readonly TextBlock sectionLabel = new() { FontSize = 12, Foreground = Muted, FontWeight = FontWeight.SemiBold };
    private readonly Button yearSwitchButton = new() { Content = "Jahrgang wechseln / neu …", Name = "SwitchYearHeader", Padding = new Thickness(10, 4), MinHeight = 28, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock statusIcon = new() { Text = "ℹ", FontSize = 14, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
    private Bitmap? schoolLogo;
    private Border statusBanner = null!;
    private FeedbackKind feedbackKind = FeedbackKind.Info;
    private bool updatingFeedback;

    internal static string FormatStatus(ReviewStatus? status) => status switch
    {
        ReviewStatus.Valid => "Formal gültig",
        ReviewStatus.NeedsReview => "Fachlich zu klären",
        ReviewStatus.Invalid => "Regelverstoß",
        _ => "Ohne Wahl"
    };
    internal static string FormatKnowledge(Knowledge value) => value switch
    {
        Knowledge.Yes => "Ja",
        Knowledge.No => "Nein",
        _ => "Unbekannt"
    };
    internal static string FormatDeparture(DepartureStatus status) => status switch
    {
        DepartureStatus.ExpectedDeparture => "Voraussichtlich",
        DepartureStatus.ConfirmedDeparture => "Bestätigt",
        _ => "Aktiv"
    };
    internal static string FormatCourseType(CourseType type) => type switch
    {
        CourseType.Lf => "LF",
        CourseType.Bf => "BF",
        CourseType.Elective => "Wahlfach",
        _ => "Seminarkurs"
    };
    internal static string FormatVariant(CourseVariant variant) => variant == CourseVariant.Bilingual ? "bilingual" : "normal";
    internal static string FormatOfferStatus(OfferStatus status) => status switch
    {
        OfferStatus.Offered => "angeboten",
        OfferStatus.Established => "fest eingerichtet",
        OfferStatus.Cancelled => "entfällt",
        _ => "geplant"
    };
    internal static string FormatRevisionSource(RevisionSource source) => source switch
    {
        RevisionSource.StudentSubmission => "Schülerabgabe",
        RevisionSource.TeacherEdit => "Lehreränderung",
        RevisionSource.OfferCancellation => "Kursstreichung",
        RevisionSource.AsvReconciliation => "Externer Abgleich",
        RevisionSource.Restoration => "Wiederherstellung",
        _ => source.ToString()
    };
    internal static string FormatActorRole(ActorRole role) => role switch
    {
        ActorRole.Student => "Schüler",
        ActorRole.Teacher => "Oberstufenberatung",
        _ => "System"
    };
    internal static string FormatReconciliationKind(ReconciliationKind kind) => kind switch
    {
        ReconciliationKind.Identical => "Identisch",
        ReconciliationKind.Changed => "Abweichend",
        ReconciliationKind.OnlyInternal => "Nur im Lehrerbestand",
        ReconciliationKind.OnlyExternal => "Nur in externer Datei",
        ReconciliationKind.InvalidIdentity => "Unklare Kennung / Dublette",
        _ => kind.ToString()
    };

    private static bool IsErrorMessage(string msg) =>
        msg.Contains("nicht abgeschlossen", StringComparison.OrdinalIgnoreCase)
        || msg.Contains("nicht gespeichert", StringComparison.OrdinalIgnoreCase)
        || msg.Contains("nicht vollständig gespeichert", StringComparison.OrdinalIgnoreCase)
        || msg.Contains("fehlgeschlagen", StringComparison.OrdinalIgnoreCase);

    private static bool IsSuccessMessage(string msg) =>
        msg.Contains("gespeichert", StringComparison.OrdinalIgnoreCase)
        || msg.Contains("verteilt", StringComparison.OrdinalIgnoreCase)
        || msg.Contains("übernommen", StringComparison.OrdinalIgnoreCase)
        || msg.Contains("abgeschlossen", StringComparison.OrdinalIgnoreCase)
        || msg.Contains("exportiert", StringComparison.OrdinalIgnoreCase)
        || msg.Contains("angelegt", StringComparison.OrdinalIgnoreCase)
        || msg.Contains("wiederhergestellt", StringComparison.OrdinalIgnoreCase)
        || msg.Contains("erstellt", StringComparison.OrdinalIgnoreCase);

    private void SetStatus(string message, FeedbackKind kind = FeedbackKind.Info)
    {
        updatingFeedback = true;
        try
        {
            feedbackKind = kind;
            status.Text = message;
            ApplyFeedbackStyle();
        }
        finally { updatingFeedback = false; }
    }

    private void SetError(string message) =>
        SetStatus(message.StartsWith("Vorgang nicht abgeschlossen:", StringComparison.Ordinal) ? message : "Vorgang nicht abgeschlossen: " + message, FeedbackKind.Error);

    private void ClearError()
    {
        if (feedbackKind == FeedbackKind.Error)
            SetStatus("", FeedbackKind.Info);
    }

    private void ApplyFeedbackStyle()
    {
        var msg = status.Text ?? "";
        if (statusBanner is null) return;
        statusBanner.IsVisible = !string.IsNullOrWhiteSpace(msg);
        statusBanner.Background = feedbackKind switch
        {
            FeedbackKind.Error => Brush.Parse("#FCEBEB"),
            FeedbackKind.Warning => Brush.Parse("#FAEEDA"),
            FeedbackKind.Success => SuccessSoft,
            _ => Brush.Parse("#EEF0F8")
        };
        statusBanner.BorderBrush = feedbackKind switch
        {
            FeedbackKind.Error => Brush.Parse("#A32D2D"),
            FeedbackKind.Warning => Brush.Parse("#C48723"),
            FeedbackKind.Success => SuccessBorder,
            _ => Line
        };
        status.Foreground = feedbackKind switch
        {
            FeedbackKind.Error => Brush.Parse("#8C1D1D"),
            FeedbackKind.Warning => Brush.Parse("#785311"),
            FeedbackKind.Success => SuccessInk,
            _ => Ink
        };
        statusIcon.Text = feedbackKind switch
        {
            FeedbackKind.Error => "⚠",
            FeedbackKind.Warning => "⚠",
            FeedbackKind.Success => "✓",
            _ => "ℹ"
        };
        statusIcon.Foreground = status.Foreground;
    }

    private void BuildVisualShell()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Background = Brush.Parse("#F3F4F8"); Foreground = Ink; FontSize = 14;
        Styles.Add(new StyleInclude(new Uri("avares://GsmKurswahl.TeacherApp/")) { Source = new Uri("avares://GsmKurswahl.TeacherApp/TeacherStyles.axaml") });
        using (var logoStream = SchoolAssets.OpenLogoStream())
            schoolLogo = new Bitmap(logoStream);

        var outer = new DockPanel();
        var topBarStack = new StackPanel();
        topBarStack.Children.Add(new Border { Height = 4, Background = Accent });

        var headerInner = new StackPanel { Spacing = 10, Margin = new Thickness(28, 12, 28, 10) };
        var brand = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var titles = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = "GSM  /  KURSWAHL · LEHRERVERWALTUNG", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Accent, LetterSpacing = 1.4 });
        var headingRow = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        heading.Margin = new Thickness(0, 0, 14, 0);
        headingRow.Children.Add(heading);
        ToolTip.SetTip(yearSwitchButton, "Zwischen eingerichteten Abiturjahrgängen wechseln, das Fachangebot ansehen oder im neuen Schuljahr einen weiteren Abiturjahrgang anlegen.");
        yearSwitchButton.Click += (_, _) => ShowManage();
        headingRow.Children.Add(yearSwitchButton);
        titles.Children.Add(headingRow);
        brand.Children.Add(titles);

        var badge = new Border { Background = Brush.Parse("#FFF1D9"), CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 6), VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "Erprobungsbetrieb", Foreground = Brush.Parse("#785311"), FontSize = 12, FontWeight = FontWeight.SemiBold } };
        ToolTip.SetTip(badge, "Nur synthetische Daten. Fachliche Freigabe offen; keine verbindliche Abgabe.");
        var logoImage = new Image
        {
            Name = "SchoolLogo",
            Source = schoolLogo,
            Height = 48,
            Width = 48 * SchoolAssets.LogoAspectRatio,
            Stretch = Stretch.Fill,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        RenderOptions.SetBitmapInterpolationMode(logoImage, BitmapInterpolationMode.HighQuality);
        AutomationProperties.SetName(logoImage, "Schullogo Gymnasium bei St. Michael Schwäbisch Hall");
        ToolTip.SetTip(logoImage, "Gymnasium bei St. Michael Schwäbisch Hall");
        var brandRight = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        brandRight.Children.Add(badge);
        brandRight.Children.Add(new Border { Width = 1, Height = 36, Background = Line, VerticalAlignment = VerticalAlignment.Center });
        brandRight.Children.Add(logoImage);
        Grid.SetColumn(brandRight, 1); brand.Children.Add(brandRight);

        headerInner.Children.Add(brand);
        headerInner.Children.Add(new Border { Height = 1, Background = Brush.Parse("#ECEEF3") });
        headerInner.Children.Add(navigation);
        topBarStack.Children.Add(headerInner);
        var topBar = new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = topBarStack };
        DockPanel.SetDock(topBar, Dock.Top); outer.Children.Add(topBar);

        var root = new DockPanel { Margin = new Thickness(28, 14, 28, 16) };
        workflowProgress.Margin = new Thickness(0, 0, 0, 12);
        DockPanel.SetDock(workflowProgress, Dock.Top); root.Children.Add(workflowProgress);

        status.FontSize = 13; status.Foreground = Ink; status.FontWeight = FontWeight.Medium;
        status.VerticalAlignment = VerticalAlignment.Center;
        var dismissAlert = new Button
        {
            Name = "DismissAlert", Content = "✕", Padding = new Thickness(6, 2), MinHeight = 24, MinWidth = 24,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(8, 0, 0, 0)
        };
        ToolTip.SetTip(dismissAlert, "Meldung ausblenden");
        dismissAlert.Click += (_, _) => { statusBanner.IsVisible = false; if (feedbackKind == FeedbackKind.Error) ClearError(); };
        var alertGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        alertGrid.Children.Add(statusIcon);
        var alertScroll = new ScrollViewer { Content = status, MaxHeight = 160 };
        Grid.SetColumn(alertScroll, 1); alertGrid.Children.Add(alertScroll);
        Grid.SetColumn(dismissAlert, 2); alertGrid.Children.Add(dismissAlert);
        statusBanner = new Border
        {
            Name = "AlertBanner",
            Background = Brush.Parse("#EEF0F8"), BorderBrush = Line, BorderThickness = new Thickness(4, 1, 1, 1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8), Margin = new Thickness(8, 0, 8, 10),
            Child = alertGrid
        };
        status.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBlock.TextProperty || updatingFeedback) return;
            var msg = status.Text ?? "";
            feedbackKind = IsErrorMessage(msg) ? FeedbackKind.Error
                : IsSuccessMessage(msg) ? FeedbackKind.Success
                : FeedbackKind.Info;
            ApplyFeedbackStyle();
        };
        ApplyFeedbackStyle();

        var footer = new StackPanel { Spacing = 2, Margin = new Thickness(0, 8, 0, 0) };
        footer.Children.Add(new TextBlock { Text = "Synthetische Daten · fachliche Freigabe offen · keine verbindliche Abgabe", FontSize = 11, Foreground = Muted });
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);

        busyPanel.Children.Add(busyText); busyPanel.Children.Add(busyProgress);
        busyPanel.Margin = new Thickness(8, 0, 8, 8);

        var content = new DockPanel();
        sectionLabel.Margin = new Thickness(8, 0, 8, 8); DockPanel.SetDock(sectionLabel, Dock.Top); content.Children.Add(sectionLabel);
        DockPanel.SetDock(busyPanel, Dock.Top); content.Children.Add(busyPanel);
        DockPanel.SetDock(statusBanner, Dock.Top); content.Children.Add(statusBanner);
        content.Children.Add(pageHost);
        root.Children.Add(Surface(content, 20));
        outer.Children.Add(root);
        Content = outer;
    }
    private static Border Surface(Control content, double padding = 16) => new()
    {
        Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(padding), Child = content
    };
    private static Border SubtleCard(Control content, double padding = 12) => new()
    {
        Background = Brush.Parse("#F7F8FB"), BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(padding), Child = content
    };
    private static Border InlineErrorBox(string name, out TextBlock text)
    {
        text = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#8C1D1D"), FontSize = 13 };
        return new Border
        {
            Name = name, Background = Brush.Parse("#FCEBEB"), BorderBrush = Brush.Parse("#A32D2D"), BorderThickness = new Thickness(4, 1, 1, 1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6), IsVisible = false, Child = text
        };
    }
    private static void SetInlineError(Border box, TextBlock text, string? message)
    {
        var has = !string.IsNullOrWhiteSpace(message);
        text.Text = has ? message : "";
        box.IsVisible = has;
    }
    private static Button Primary(Button button) { button.Classes.Add("primary"); return button; }
    private static Button Compact(Button button) { button.Classes.Add("compact"); return button; }
    private static Button Danger(Button button) { button.Classes.Add("danger"); return button; }
    private void UpdateVisualNavigation()
    {
        yearSwitchButton.IsVisible = workspace?.Active is not null && visiblePage != "new";
        workflowProgress.IsVisible = visiblePage is "wahl" or "offer" or "import" or "profiles" or "collection" or "people" or "new";
        sectionLabel.Text = visiblePage switch
        {
            "offer" => "EINRICHTUNG  /  FACHANGEBOT", "import" => "SCHÜLERLISTE  /  IMPORT & ABGLEICH",
            "profiles" => "SCHÜLERDATEIEN  /  VERTEILUNG  (PÄDAGOGISCHES NETZ)", "collection" => "KURSWAHLEN  /  EINGANG & KONFLIKTE  (PÄDAGOGISCHES NETZ)",
            "wahl" => "WAHL / VORBEREITEN & EINSAMMELN", "results" => "KURSWAHLEN / PRÜFEN, ÄNDERN, DRUCKEN", "people" => "WAHL / SCHÜLERBESTAND", "settings" => "EINSTELLUNGEN", "help" => "HILFE & DATEIEN",
            "planning" => "KURSPLANUNG / SZENARIEN & ANALYSE", "manage" => "JAHRGANG VERWALTEN", "catalog" => "FACHANGEBOT", "new" => "WILLKOMMEN", "asv" => "ASV-ABGLEICH", _ => "JAHRGANGSÜBERSICHT"
        };
        foreach (var button in navigation.Children.OfType<Button>())
        {
            button.Classes.Add("nav");
            var selected = button.Content?.ToString() switch
            {
                "Übersicht" => visiblePage == "start", "Wahl" => visiblePage is "wahl" or "offer" or "import" or "profiles" or "collection" or "people",
                "Kurswahlen" => visiblePage == "results",
                "Kursplanung" => visiblePage == "planning",
                "ASV-Abgleich" => visiblePage == "asv",
                "Jahrgang verwalten" => visiblePage is "manage" or "catalog", "Einstellungen" => visiblePage == "settings",
                "Hilfe" => visiblePage == "help", "Zur Einrichtung" => visiblePage is "new" or "offer" or "import" or "profiles", _ => false
            };
            button.Classes.Set("selected", selected);
        }
    }
    private void UpdateWorkflowProgress()
    {
        workflowProgress.Children.Clear();
        var active = workspace?.Active;
        if (active is null && visiblePage != "new") return;
        var summary = active is not null && store is not null && session is not null ? TeacherDashboardBuilder.Build(store, session) : null;
        var setup = active?.Step;
        var stage = setup switch { YearSetupStep.Offer => 0, YearSetupStep.Import => 1, YearSetupStep.Mapping or YearSetupStep.Review => 2, _ => -1 };
        var distributed = summary is { Students: > 0 } && summary.Delivered == summary.Students;
        string[] titles = ["Fächer auswählen", "Schülerliste importieren", "Dateien verteilen (päd. Netz)", "Wahlen einsammeln (päd. Netz)"];
        string[] descriptions = ["Leistungsfachangebot festlegen (Verwaltungsnetz)", "Liste gemeinsam übernehmen (Verwaltungsnetz)", "Ins pädagogische Netz wechseln, Laufwerk wählen und zustellen", "Im pädagogischen Netz einsammeln und danach Rück-Transportdatei sichern"];
        var strip = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,*,16,*,16,*") };
        for (var i = 0; i < 4; i++)
        {
            var stepIndex = i;
            var complete = active is not null && (i == 0 ? setup != YearSetupStep.Offer : i == 1 ? stage > 1 || setup == YearSetupStep.Complete && summary?.Students > 0 : i == 2 ? distributed : summary is { Students: > 0 } && summary.Choices == summary.Students && collections.IsEmpty);
            var current = stage == i || setup == YearSetupStep.Complete && (i == 2 && !distributed || i == 3 && distributed && !complete);
            var viewing = i switch
            {
                0 => visiblePage is "offer" or "catalog",
                1 => visiblePage == "import",
                2 => visiblePage == "profiles",
                3 => visiblePage == "collection",
                _ => false
            };
            var state = current ? "Aktueller Schritt" : complete ? "Erledigt" : "Noch offen";
            if (setup == YearSetupStep.Complete && i == 2 && !distributed) state = "Zustellungen offen";
            var caption = new StackPanel { Spacing = 3 };
            caption.Children.Add(new TextBlock { Text = titles[i], FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = viewing || current ? Accent : Ink });
            caption.Children.Add(new TextBlock { Name = "WorkflowState" + i, Text = state, FontSize = 12, Foreground = viewing || current || complete ? Accent : Muted });
            var number = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Background = current || viewing ? Accent : complete ? SoftAccent : Brush.Parse("#ECEEF4"), VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = complete && !current ? "✓" : (i + 1).ToString(), Foreground = current || viewing ? Brushes.White : complete ? Accent : Muted, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("42,*") }; row.Children.Add(number); Grid.SetColumn(caption, 1); row.Children.Add(caption);
            var tile = Surface(row, 12); tile.Name = "WorkflowStep" + i;
            tile.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            tile.Background = viewing ? SoftAccent : current ? HighlightAccent : Brushes.White;
            tile.BorderBrush = viewing || current ? Accent : Line;
            tile.BorderThickness = new Thickness(viewing ? 2 : 1);
            tile.PointerPressed += (_, _) => JumpToWorkflowStep(stepIndex);
            AutomationProperties.SetName(tile, $"Schritt {i + 1}: {titles[i]}. {state}.");
            ToolTip.SetTip(tile, descriptions[i] + ". Anklicken, um direkt zu diesem Schritt zu wechseln. Die Anzeige beschreibt den Arbeitsablauf, keine fachliche Freigabe.");
            Grid.SetColumn(tile, i * 2); strip.Children.Add(tile);
            if (i < 3)
            {
                var connector = new Border { Height = 2, Background = complete ? Accent : Line, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(connector, i * 2 + 1); strip.Children.Add(connector);
            }
        }
        workflowProgress.Children.Add(strip);
    }
    private Control SummaryCards(TeacherDashboard summary)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
        (string Value, string Label, string Tip, Action Open)[] values = [
            (summary.Students.ToString(), "Schüler im Jahrgang", "Gesamtzahl der für diesen Abiturjahrgang im Lehrerbestand gespeicherten Personen.", () => ShowPage("people")),
            ($"{summary.Delivered} / {summary.Students}", "Profile zugestellt", "Anzahl der Personen, deren aktuelles persönliches Profil (profil.json) und Fachangebot erfolgreich im Wahlordner abgelegt wurden.", () => ShowPage("profiles")),
            (summary.Choices.ToString(), "Wahlen eingelesen", "Anzahl der Personen, für die bereits eine Kurswahl im Lehrerbestand vorliegt.", () => { choiceFilter = new(); ShowResults(); }),
            (summary.Reviews.ToString(), "Fachlich zu klären", "Wahlen mit offenen Vorbildungsfragen, Sonderfällen (z. B. Sportersatz, Religion/Ethik) oder Regelverstößen.", () => { choiceFilter = new(State: ChoiceStateFilter.NeedsReview); ShowResults(); })];
        if (summary.Students > 0 && summary.Choices == summary.Students)
            values = [
                (summary.Students.ToString(), "Schüler im Jahrgang", "Gesamtzahl der für diesen Abiturjahrgang im Lehrerbestand gespeicherten Personen.", () => ShowPage("people")),
                (summary.Choices.ToString(), "Wahlen eingelesen", "Alle Schüler dieses Jahrgangs haben eine erfasste Kurswahl.", () => { choiceFilter = new(); ShowResults(); }),
                (summary.Reselections.ToString(), "Offene Nachwahlen", "Schüler, die nach einer Kursstreichung oder wegen eines Regelverstoßes erneut wählen müssen.", () => { choiceFilter = new(State: ChoiceStateFilter.ReselectionOpen); ShowResults(); }),
                (summary.Reviews.ToString(), "Fachlich zu klären", "Wahlen mit offenen Vorbildungsfragen, Sonderfällen oder Regelverstößen.", () => { choiceFilter = new(State: ChoiceStateFilter.NeedsReview); ShowResults(); })];
        for (var i = 0; i < values.Length; i++)
        {
            var entry = values[i];
            var body = new StackPanel { Spacing = 5 };
            body.Children.Add(new TextBlock { Text = entry.Value, FontSize = 28, FontWeight = FontWeight.SemiBold, Foreground = i == 3 && summary.Reviews > 0 ? Brush.Parse("#946312") : Accent });
            body.Children.Add(new TextBlock { Text = entry.Label, FontSize = 12, Foreground = Muted, TextWrapping = TextWrapping.Wrap });
            var card = Surface(body); card.Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0);
            card.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            card.PointerPressed += (_, _) => entry.Open();
            AutomationProperties.SetName(card, $"{entry.Label}: {entry.Value}");
            ToolTip.SetTip(card, entry.Tip + "\n\nKlicken zum Öffnen.");
            Grid.SetColumn(card, i); grid.Children.Add(card);
        }
        return grid;
    }
}
