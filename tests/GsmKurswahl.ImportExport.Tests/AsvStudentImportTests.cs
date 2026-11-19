using System.Collections.Immutable;
using GsmKurswahl.Domain;

namespace GsmKurswahl.ImportExport.Tests;

/// <summary>ASV-BW format „GSM Export für Kurswahl“ – exclusively synthetic persons and demo IDs.</summary>
public sealed class AsvStudentImportTests
{
    private static ImportOrigin Origin => new("asv-demo.xlsx", "synthetic-hash", AsvStudentImport.MappingVersion, DateTimeOffset.UtcNow, "demo-beratung");

    private const string Csv =
        "ASV-ID;Familienname;Rufname;Klasse;Klassengruppe;Profil;Unterrichtsfächer\n" +
        "demo-asv-1;Muster;Demo;10a;bili;I;REV, D, M, E, F, I, BIO\n" +
        "demo-asv-2;Beispiel;Testa;10b;reg;IMP;RRK, D, M, E, L, IMP\n" +
        "demo-asv-3;Probe;Fiktiv;10c;reg;NWT;ETH, D, M, E, F, NWT, BRK-INF\n" +
        "demo-asv-4;Leer;Unbekannt;10c;;;\n" +
        "demo-asv-5;Doppel;Religion;10a;reg;NWT;REV, ETH, D, E, F\n";

    private static ImmutableArray<StudentImportRow> Preview(ImportTable source)
    {
        var converted = AsvStudentImport.Convert(source, 2029);
        return StudentTableImport.Preview(converted, StudentColumnMatcher.Match(converted.Headers), Origin, "demo-beratung");
    }

    [Fact]
    public void ConvertsProfileReligionLanguagesBridgeAndBilingualFromAsvCodes()
    {
        var table = StudentTableImport.Csv(Csv);
        Assert.True(AsvStudentImport.Detect(table));
        var rows = Preview(table);

        var italian = rows[0].Candidate!.Student;
        Assert.Equal("demo-asv-1", italian.ExternalStudentId);
        Assert.Equal(2029, italian.GraduationYear);
        Assert.Equal("10a/Muster.Demo", rows[0].Candidate!.HomeDirectory);
        Assert.Equal(Knowledge.Yes, italian.Prerequisites.ItalianProfile);
        Assert.Equal(Knowledge.No, italian.Prerequisites.InformaticsImp);
        Assert.Equal(Knowledge.No, italian.Prerequisites.InformaticsBridge);
        Assert.Equal(Knowledge.Yes, italian.Prerequisites.ContinuousBilingual);
        Assert.Equal("ER", italian.Prerequisites.ReligionEthicsGrade10);
        Assert.Equal(new[] { "E", "F", "I" }, italian.Prerequisites.ContinuedLanguages.ToArray());
        Assert.Null(italian.Prerequisites.SportExemptSemesters);

        var imp = rows[1].Candidate!.Student.Prerequisites;
        Assert.Equal((Knowledge.No, Knowledge.Yes, Knowledge.No, "KR"), (imp.ItalianProfile, imp.InformaticsImp, imp.ContinuousBilingual, imp.ReligionEthicsGrade10));
        Assert.Equal(new[] { "E", "L" }, imp.ContinuedLanguages.ToArray());

        var bridge = rows[2].Candidate!.Student.Prerequisites;
        Assert.Equal((Knowledge.No, Knowledge.No, Knowledge.Yes, "ETH"), (bridge.ItalianProfile, bridge.InformaticsImp, bridge.InformaticsBridge, bridge.ReligionEthicsGrade10));
    }

    [Fact]
    public void EmptyAsvFieldsStayUnknownAndAmbiguousReligionIsRejected()
    {
        var rows = Preview(StudentTableImport.Csv(Csv));
        var empty = rows[3].Candidate!.Student.Prerequisites;
        Assert.Equal(Knowledge.Unknown, empty.ItalianProfile);
        Assert.Equal(Knowledge.Unknown, empty.InformaticsImp);
        Assert.Equal(Knowledge.Unknown, empty.InformaticsBridge);
        Assert.Equal(Knowledge.Unknown, empty.ContinuousBilingual);
        Assert.Null(empty.ReligionEthicsGrade10);
        Assert.Empty(empty.ContinuedLanguages);

        Assert.Null(rows[4].Candidate);
        Assert.Contains("Religion", rows[4].Error);
    }

    [Fact]
    public void RecognizesAsvDefaultFieldNamesAndRejectsOtherTables()
    {
        var defaults = StudentTableImport.Csv(
            "Klasse;Rufname;Familienname;Schüler-ID;1. Schülerprofil Profilfach.Kurzform;Alle besuchten Unterrichtsfächer;Klassengruppe\n" +
            "10a;Demo;Muster;demo-asv-9;IMP;ETH, D, E, F, IMP;bili\n");
        Assert.True(AsvStudentImport.Detect(defaults));
        var row = Preview(defaults).Single();
        Assert.Empty(row.Error);
        Assert.Equal(Knowledge.Yes, row.Candidate!.Student.Prerequisites.InformaticsImp);
        Assert.Equal(Knowledge.Yes, row.Candidate.Student.Prerequisites.ContinuousBilingual);

        var internalTable = StudentTableImport.Csv("studentId;name;class;year\ndemo-001;Demo Eins;10a;2029\n");
        Assert.False(AsvStudentImport.Detect(internalTable));
        Assert.Throws<InvalidDataException>(() => AsvStudentImport.Convert(internalTable, 2029));

        var withoutName = StudentTableImport.Csv("ASV-ID;Klasse;Profil;Unterrichtsfächer\ndemo-asv-1;10a;NWT;D\n");
        Assert.Throws<InvalidDataException>(() => AsvStudentImport.Convert(withoutName, 2029));
    }

    [Fact]
    public void RealAsvIdsRemainBlockedDuringTrialOperation()
    {
        var table = StudentTableImport.Csv("ASV-ID;Familienname;Rufname;Klasse;Klassengruppe;Profil;Unterrichtsfächer\n00000000-0000-0000-0000-000000000001;Muster;Demo;10a;reg;NWT;ETH, D\n");
        var row = Preview(table).Single();
        Assert.Null(row.Candidate);
        Assert.Contains("demo-IDs", row.Error);
    }
}
