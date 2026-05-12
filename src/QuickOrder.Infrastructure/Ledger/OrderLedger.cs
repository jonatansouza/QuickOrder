namespace QuickOrder.Infrastructure.Ledger;

using System.Collections.Concurrent;

public record LedgerEntry(
    DateTime Timestamp,
    string MsgType,
    string ClOrdId,
    string OrdStatus,
    string? Text
);

public class OrderLedger
{
    private readonly ConcurrentQueue<LedgerEntry> _entries = new();

    public void Append(LedgerEntry entry) => _entries.Enqueue(entry);

    public IReadOnlyList<LedgerEntry> Snapshot() => _entries.ToArray();

    public int Count => _entries.Count;
}
