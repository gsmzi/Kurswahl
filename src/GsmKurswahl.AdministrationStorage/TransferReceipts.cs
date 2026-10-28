using System.Collections.Immutable;
using GsmKurswahl.Domain;

namespace GsmKurswahl.AdministrationStorage;

public sealed partial class SqliteAdministrationStore
{
    public bool TransferTrackingEnabled => Convert.ToInt32(Scalar("PRAGMA user_version")) == 3;
    public void EnableTransferTracking() { }
    public ImmutableArray<TransferReceipt> TransferReceipts()
    {
        if (!TransferTrackingEnabled) return [];
        var rows = Read<TransferReceipt>("SELECT body FROM transfer_receipts ORDER BY seq");
        if (rows.Any(r => r.SchemaVersion != 1)) throw new InvalidDataException("Unbekannte Übertragungsvermerksversion.");
        return rows;
    }
    public void RecordTransfer(TransferReceipt receipt)
    {
        if (!TransferTrackingEnabled) throw new InvalidOperationException("Unbekannte Verwaltungsschemaversion.");
        if (receipt.SchemaVersion != 1 || receipt.Id == Guid.Empty || receipt.At == default || string.IsNullOrWhiteSpace(receipt.Actor) || string.IsNullOrWhiteSpace(receipt.Note))
            throw new ArgumentException("Unvollständiger Übertragungsvermerk.");
        using var tx = db.BeginTransaction();
        var current = History(receipt.StudentId).LastOrDefault();
        if (current?.Id != receipt.ChoiceRevisionId || current.OfferVersion != receipt.CatalogVersion || current.RuleVersion != receipt.RuleVersion
            || Students().Single(s => s.Student.ExternalStudentId == receipt.StudentId).Version != receipt.StudentVersion)
            throw new InvalidOperationException("Wahl oder Stammdaten geändert; Ansicht erneuern.");
        Execute("INSERT INTO transfer_receipts(body) VALUES($p0)", Json(receipt)); tx.Commit();
    }
}
