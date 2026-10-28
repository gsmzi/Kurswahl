namespace GsmKurswahl.Domain;

// A human records a manual ASV entry, never a legal approval or an ASV API response.
public sealed record TransferReceipt(int SchemaVersion, Guid Id, string StudentId, Guid ChoiceRevisionId,
    int StudentVersion, string CatalogVersion, string RuleVersion, DateTimeOffset At, string Actor,
    bool Entered, string Note);
