using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using GsmKurswahl.Application;

namespace GsmKurswahl.Application.Tests;

public sealed class ProgramVersionTests
{
    private static string Repository([CallerFilePath] string source = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));

    [Fact]
    public void VersionStartsWithVersionFileAndCarriesBuildNumberAndCommit()
    {
        var baseVersion = File.ReadAllText(Path.Combine(Repository(), "version.txt")).Trim();
        var full = ProgramVersion.Read(typeof(ProgramVersion).Assembly);
        Assert.Matches("^" + Regex.Escape(baseVersion) + @"\.\d+\+([0-9a-f]+(\.geaendert)?|unbekannt)$", full);
        Assert.Equal(typeof(ProgramVersion).Assembly.GetName().Version!.ToString(2), baseVersion);
    }
}
