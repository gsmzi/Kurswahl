using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using GsmKurswahl.TeacherApp;
using GsmKurswahl.ImportExport;

namespace GsmKurswahl.StudentApp.Tests;

public class TeacherWindowTests
{
    [AvaloniaFact]
    public void TeacherWindowStartsAndExposesAdministrativeWorkflow()
    {
        var window = new TeacherWindow();
        try
        {
            window.Show(); window.UpdateLayout();
            Assert.Contains("M3", window.Title);
            var tabs = window.GetLogicalDescendants().OfType<TabControl>().Single();
            Assert.Equal(6, tabs.ItemCount);
            Assert.Equal("Start & Dateien", Assert.IsType<TabItem>(tabs.SelectedItem).Header);
            Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Welche Datei brauche ich wofür?");
            for (var i = 0; i < tabs.ItemCount; i++) { tabs.SelectedIndex = i; window.UpdateLayout(); Assert.NotNull(tabs.SelectedContent); }
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public void ColumnPickerMapsSelectedHeadersAndClearsPreviousFile()
    {
        var picker = new ImportColumnPicker();
        var window = new Window { Content = picker };
        try
        {
            window.Show();
            picker.Load(new ImportTable(["studentId", "Anzeigename", "IMP"], []));
            var boxes = picker.GetLogicalDescendants().OfType<ComboBox>().ToDictionary(b => b.Name!);
            Assert.Equal("studentId", picker.Read()["studentId"]);
            Assert.DoesNotContain("imp", picker.Read().Keys);
            boxes["Column_name"].SelectedIndex = 2;
            boxes["Column_imp"].SelectedIndex = 3;
            Assert.Equal("Anzeigename", picker.Read()["name"]);
            Assert.Equal("IMP", picker.Read()["imp"]);
            Assert.NotNull(ToolTip.GetTip(boxes["Column_imp"]));
            picker.Load(new ImportTable(["Andere Spalte"], []));
            Assert.Empty(picker.Read());
            picker.Clear(); Assert.Empty(picker.Read());
        }
        finally { window.Close(); }
    }
}
