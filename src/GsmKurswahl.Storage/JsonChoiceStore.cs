using System.Security.Cryptography;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Storage;

public sealed class ChoiceConflictException(string message) : IOException(message);

public interface IFileCommitter
{
    void Commit(string temporaryPath, string targetPath, bool exists);
}
public sealed class AtomicFileCommitter : IFileCommitter
{
    public void Commit(string temporaryPath, string targetPath, bool exists)
    {
        if (exists) File.Replace(temporaryPath, targetPath, targetPath + ".bak");
        else File.Move(temporaryPath, targetPath, overwrite: false);
    }
}

public sealed class JsonChoiceStore(string path, IFileCommitter? committer = null) : IChoiceStore
{
    private const int MaximumBytes = 2 * 1024 * 1024;
    private readonly string target = Path.GetFullPath(path);
    private readonly IFileCommitter fileCommitter = committer ?? new AtomicFileCommitter();

    public async Task<StoredChoice?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await ReadBoundedAsync(target, cancellationToken);
            return new(JsonDocuments.ReadChoice(bytes), Token(bytes));
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        // Do not turn malformed data, denied access or an unavailable drive into an empty draft.
    }

    public async Task<StoredChoice> SaveAsync(CourseChoice choice, string? expectedToken, CancellationToken cancellationToken = default)
    {
        var bytes = JsonDocuments.WriteChoice(choice);
        if (bytes.Length > MaximumBytes) throw new IOException("Wahldatei überschreitet 2 MiB.");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        // All writers in this application cooperate through this exclusive lock. The file may remain after a crash.
        using var writeLock = new FileStream(target + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var current = await LoadAsync(cancellationToken);
        if (current?.ContentToken != expectedToken)
            throw new ChoiceConflictException("Die Datei wurde zwischenzeitlich geändert. Neu laden; Entwurf vorher sichern.");
        if (current is not null && (current.Choice.DocumentId != choice.DocumentId || current.Choice.StudentId != choice.StudentId
            || current.Choice.GraduationYear != choice.GraduationYear || choice.Revision != current.Choice.Revision + 1))
            throw new ChoiceConflictException("Identität oder Revisionsfolge stimmt nicht mit dem geladenen Entwurf überein.");
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            fileCommitter.Commit(temporary, target, current is not null);
            return new(choice, Token(bytes));
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string Token(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static async Task<byte[]> ReadBoundedAsync(string file, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
        if (stream.Length > MaximumBytes) throw new IOException("Wahldatei überschreitet 2 MiB.");
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(block, cancellationToken)) > 0)
        {
            if (buffer.Length + count > MaximumBytes) throw new IOException("Wahldatei überschreitet 2 MiB.");
            buffer.Write(block, 0, count);
        }
        return buffer.ToArray();
    }
}
