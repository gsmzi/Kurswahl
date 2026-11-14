namespace GsmKurswahl.AdministrationStorage;

public sealed class TeacherProgramInUseException() : IOException("Das Lehrerprogramm wird bereits verwendet. Bitte schließen Sie es am anderen Arbeitsplatz und starten Sie es anschließend erneut.");

/// <summary>One regular teacher process per installation, including across an SMB share.</summary>
public sealed class TeacherProgramLease : IDisposable
{
    public const string FileName = "lehrerprogramm.sperre";
    private readonly FileStream handle;

    public TeacherProgramLease(string programDirectory)
    {
        // Ship this file with the program: read-only access permits a protected program share.
        // Never create, replace or delete it here. Its open handle, not its existence, is the lock.
        try
        {
            handle = new FileStream(Path.Combine(Path.GetFullPath(programDirectory), FileName),
                FileMode.Open, FileAccess.Read, FileShare.None);
        }
        catch (IOException ex) when (OperatingSystem.IsWindows() && (ex.HResult & 0xffff) is 32 or 33)
        {
            throw new TeacherProgramInUseException();
        }
    }

    public void Dispose() => handle.Dispose();
}
