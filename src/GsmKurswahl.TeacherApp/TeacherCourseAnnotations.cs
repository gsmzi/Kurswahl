using Avalonia.Controls;
using Avalonia.Layout;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;

namespace GsmKurswahl.TeacherApp;

public sealed partial class TeacherWindow
{
    private Expander CourseAnnotationEditor()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Text("Markierungen dienen nur der Übersicht und erscheinen auf den Ausdrucken. Koopstandort und Aufsetzerkurs sind unabhängig voneinander. Wahlen, Stunden und Prüfungen ändern sich dadurch nicht."));
        panel.Children.Add(Text("Nach einer Änderung erneut verteilen, damit auch Schülerprogramm und Schülerausdrucke die neuen Hinweise zeigen."));
        foreach (var offer in session!.Catalog.Offers.OrderBy(o => SubjectName(o.SubjectId), StringComparer.CurrentCulture).ThenBy(o => o.Type).ThenBy(o => o.Variant))
        {
            var school = new ComboBox { Name = "Coop_" + offer.Id.Replace('-', '_'), Width = 210,
                ItemsSource = new[] { "Kein Koopkurs", "Koopkurs am EWG", "Koopkurs am GSM" }, SelectedIndex = (int)offer.CooperationSchool };
            var topUp = new CheckBox { Name = "TopUp_" + offer.Id.Replace('-', '_'), Content = "Aufsetzerkurs", IsChecked = offer.IsTopUpCourse };
            void Save()
            {
                try
                {
                    var basis = session.Catalog;
                    var next = CatalogConfiguration.Annotate(basis, offer.Id, (CooperationSchool)school.SelectedIndex, topUp.IsChecked == true);
                    if (store is SqliteAdministrationStore sql && sql.ActivePlanningCatalog() is not null)
                        sql.SaveCourseAnnotations(basis, next, Actor);
                    workspace!.Update(workspace.Active! with { Catalog = next });
                    session = new TeacherSession(store!, next, Actor);
                    status.Text = "Kursmarkierung gespeichert. Für Schüleransicht und Schülerausdruck bitte erneut verteilen.";
                }
                catch (Exception ex)
                {
                    status.Text = "Kursmarkierung nicht vollständig gespeichert: " + ex.Message + " Bitte Jahrgang neu öffnen.";
                    school.IsEnabled = topUp.IsEnabled = false;
                }
            }
            school.SelectionChanged += (_, _) => Save();
            topUp.IsCheckedChanged += (_, _) => Save();
            panel.Children.Add(CompactLine("*,220,160", [Text(SubjectName(offer.SubjectId) + " · " + FormatCourseType(offer.Type)
                + (offer.Variant == CourseVariant.Bilingual ? " · bilingual" : "")), school, topUp]));
        }
        return new Expander { Name = "CourseAnnotations", Header = "Koopkurse und Aufsetzerkurse markieren", Content = panel };
    }
}
