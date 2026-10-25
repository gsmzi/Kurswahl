using System.Collections.Immutable;
using GsmKurswahl.Domain;

namespace GsmKurswahl.ImportExport;

public enum ImportConflictKind { NewStudent, DuplicateIdentity, ChangedChoice, UntrustedOrigin, StaleRevision }
public sealed record ColumnMapping(string SourceColumn, string TargetField);
public sealed record ImportConflict(int Row, ImportConflictKind Kind, string Explanation);

// Deliberately a preview-only contract. Import cannot mutate the protected teacher store in M1.
public sealed record ImportPreview(ImportOrigin Origin, ImmutableArray<ColumnMapping> Mapping,
    ImmutableArray<CourseChoice> Candidates, ImmutableArray<ImportConflict> Conflicts)
{
    public bool RequiresConfirmation => true;
}
public interface IChoiceImportAdapter
{
    Task<ImportPreview> PreviewAsync(Stream source, ImmutableArray<ColumnMapping> mapping,
        ImportOrigin origin, CancellationToken cancellationToken = default);
}
