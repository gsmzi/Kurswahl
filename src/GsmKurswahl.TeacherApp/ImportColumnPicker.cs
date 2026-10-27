using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GsmKurswahl.ImportExport;

namespace GsmKurswahl.TeacherApp;

/// <summary>Human-readable column assignment; the import adapter retains validation responsibility.</summary>
public sealed class ImportColumnPicker : StackPanel
{
    private readonly Dictionary<string, ComboBox> columns = [];
    private static readonly (string Field, string Label, string Help)[] Fields =
    [
        ("studentId", "Schüler-ID *", "Eindeutige Kennung pro Person. Im Erprobungsbetrieb z. B. demo-001."),
        ("firstName", "Vorname", "Zusammen mit Nachname für den Standardordner Nachname.Vorname."),
        ("lastName", "Nachname", "Zusammen mit Vorname für den Standardordner Nachname.Vorname."),
        ("folder", "Abweichender Schülerordner (optional)", "Nur der Ordnername innerhalb der Klasse, z. B. Muster.Demo2. Überschreibt Nachname.Vorname."),
        ("name", "Anzeigename (alternativ Vor-/Nachname)", "Anzeigename der Person. In Testdateien ausschließlich erfundene Namen verwenden."),
        ("class", "Klasse *", "Klassenbezeichnung, z. B. 10-demo."),
        ("year", "Abiturjahrgang *", "Vierstellige Jahreszahl, passend zum geöffneten Fachangebot."),
        ("username", "Windows-Benutzername *", "Schulisches Konto in der Form DOMÄNE\\Benutzername; die Zuordnung wird anschließend geprüft."),
        ("accountId", "Windows-Konto-ID (SID)", "Eindeutige technische Kontokennung. Von der Schul-IT bestätigen lassen; nicht selbst erfinden."),
        ("home", "Persönlicher Wahlordner", "Optionaler expliziter Ordner, absolut oder relativ zum gewählten Laufwerk. Sonst wird Klasse/Nachname.Vorname verwendet. Die Verteilung prüft die Ziele gemeinsam."),
        ("italian", "Italienisch-Profil", "ja/nein; leer bedeutet unbekannt. Bestätigt das Profil, nicht automatisch die fortgeführte Sprachlaufbahn."),
        ("bridge", "Informatik-Brückenkurs", "Brückenkurs besucht: ja/nein; leer bedeutet unbekannt."),
        ("imp", "IMP besucht", "IMP besucht: ja/nein; leer bedeutet unbekannt. Getrennt vom Brückenkurs erfassen."),
        ("bilingual", "Durchgängig bilingual", "Durchgängiger bilingualer Bildungsgang: ja/nein; leer bedeutet unbekannt."),
        ("religion", "Religion/Ethik in Klasse 10", "Fachkennung ETH, ER oder KR; leer bedeutet unbekannt."),
        ("sport", "Sportbefreiung: Halbjahre", "Halbjahre mit Komma trennen, z. B. 1,2. keine bestätigt keine Befreiung; leer bedeutet unbekannt. Keine Diagnosen."),
        ("languages", "Bestätigte Sprachlaufbahnen", "Fachkennungen fortgeführter Sprachen mit Komma trennen, z. B. E,F. Nur bestätigte Angaben übernehmen."),
        ("religionPermission", "Religion/Ethik: Belegung freigegeben", "Administrative Bestätigung: ja/nein; leer bedeutet unbekannt."),
        ("religionExamPermission", "Religion/Ethik: Prüfung freigegeben", "Administrative Bestätigung für die mündliche Prüfung: ja/nein; leer bedeutet unbekannt."),
        ("permanentSport", "Dauerhafte Sportbefreiung", "ja/nein; leer bedeutet unbekannt. Keine medizinischen Details."),
        ("departure", "Abgängerstatus", "aktiv, voraussichtlich oder bestaetigt. Leer wird als aktiv übernommen.")
    ];

    public ImportColumnPicker() { Spacing = 8; Clear(); }
    public void Clear()
    {
        columns.Clear(); Children.Clear();
        Children.Add(new TextBlock { Text = "Nach Auswahl einer Schülerliste erscheinen hier deren Spalten zur Zuordnung.", TextWrapping = TextWrapping.Wrap });
    }
    public void Load(ImportTable table)
    {
        columns.Clear(); Children.Clear();
        foreach (var (field, label, help) in Fields)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("260,*"), Margin = new Thickness(0, 2) };
            var caption = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            var picker = new ComboBox { Name = "Column_" + field, HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = new[] { "— Nicht zuordnen —" }.Concat(table.Headers).ToArray(),
                SelectedIndex = table.Headers.IndexOf(field) + 1 };
            Avalonia.Automation.AutomationProperties.SetName(picker, label);
            ToolTip.SetTip(picker, new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
            ToolTip.SetTip(caption, help);
            Grid.SetColumn(picker, 1); row.Children.Add(caption); row.Children.Add(picker); Children.Add(row);
            columns.Add(field, picker);
        }
    }
    public Dictionary<string, string> Read() => columns.Where(pair => pair.Value.SelectedIndex > 0)
        .ToDictionary(pair => pair.Key, pair => (string)pair.Value.SelectedItem!);
}
