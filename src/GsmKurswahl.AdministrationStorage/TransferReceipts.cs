using System.Collections.Immutable;
using GsmKurswahl.Domain;

namespace GsmKurswahl.AdministrationStorage;

public sealed partial class SqliteAdministrationStore
{
    public bool TransferTrackingEnabled => Convert.ToInt32(Scalar("PRAGMA user_version")) == 3;
    public void EnableTransferTracking()
    {
        if (TransferTrackingEnabled) return;
        if (!PlanningEnabled) throw new InvalidOperationException("Zuerst Kursplanung aktivieren (Schema 2).");
        using var tx = db.BeginTransaction();
        Execute("CREATE TABLE transfer_receipts (seq INTEGER PRIMARY KEY, body TEXT NOT NULL); CREATE TRIGGER transfer_receipts_no_update BEFORE UPDATE ON transfer_receipts BEGIN SELECT RAISE(ABORT,'append-only'); END; CREATE TRIGGER transfer_receipts_no_delete BEFORE DELETE ON transfer_receipts BEGIN SELECT RAISE(ABORT,'append-only'); END; PRAGMA user_version=3;");
        tx.Commit();
    }
    public ImmutableArray<TransferReceipt> TransferReceipts()
    {
        if (!TransferTrackingEnabled) return [];
        var rows = Read<TransferReceipt>("SELECT body FROM transfer_receipts ORDER BY seq");
        if (rows.Any(r => r.SchemaVersion != 1)) throw new InvalidDataException("Unbekannte Übertragungsvermerksversion.");
        return rows;
    }
    public void RecordTransfer(TransferReceipt receipt)
    {
        if (!TransferTrackingEnabled) throw new InvalidOperationException("Übertragungsvermerke zuerst aktivieren (Schema 3).");
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
