using System.Collections.Concurrent;

namespace SellingNewProduct.Infrastructure.Messaging.Outbox;

/// <summary>One line of dispatch history — what the outbox relay did with a single message.</summary>
public sealed record OutboxActivityEntry(
    DateTime OccurredUtc,
    Guid MessageId,
    string MessageType,
    string Route,
    string Destination,
    string Status,      // "published" | "failed"
    string? Error);

/// <summary>
/// A small, thread-safe RING BUFFER of the most recent outbox dispatch results, kept in memory for a
/// diagnostics endpoint. Backed by a <see cref="ConcurrentQueue{T}"/>: the dispatcher (and, later, any
/// extra producers) can <see cref="Record"/> without locking, while a request thread takes a
/// <see cref="Snapshot"/> at the same time — a lock-free multi-writer / reader structure. It is bounded:
/// once it passes <see cref="Capacity"/> the oldest entries are dequeued, so memory stays flat.
///
/// This is DIFFERENT from <see cref="IOutboxSignal"/>: a Channel is a producer/consumer HAND-OFF (each
/// item consumed once); a ConcurrentQueue here is a shared observable BUFFER (kept, trimmed, read many
/// times). Two complementary concurrency tools.
/// </summary>
public interface IOutboxActivityLog
{
    void Record(OutboxActivityEntry theEntry);

    /// <summary>Most recent first, at most <paramref name="theMax"/> entries.</summary>
    IReadOnlyList<OutboxActivityEntry> Snapshot(int theMax = 50);
}

/// <inheritdoc cref="IOutboxActivityLog"/>
public sealed class OutboxActivityLog : IOutboxActivityLog
{
    private const int Capacity = 200;

    private readonly ConcurrentQueue<OutboxActivityEntry> myEntries = new();

    public void Record(OutboxActivityEntry theEntry)
    {
        myEntries.Enqueue(theEntry);

        // Trim from the front to keep at most Capacity. TryDequeue is safe even if another thread races
        // us here — worst case we drop one extra old entry, which is fine for a diagnostics buffer.
        while (myEntries.Count > Capacity && myEntries.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<OutboxActivityEntry> Snapshot(int theMax = 50)
    {
        // ToArray on a ConcurrentQueue is an atomic, consistent snapshot. Newest last in the queue,
        // so reverse to show newest first.
        var aAll = myEntries.ToArray();
        var aTake = theMax < aAll.Length ? theMax : aAll.Length;

        var aResult = new OutboxActivityEntry[aTake];
        for (var i = 0; i < aTake; i++)
        {
            aResult[i] = aAll[aAll.Length - 1 - i];
        }

        return aResult;
    }
}
