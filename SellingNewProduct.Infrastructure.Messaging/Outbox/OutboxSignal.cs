using System.Threading.Channels;

namespace SellingNewProduct.Infrastructure.Messaging.Outbox;

/// <summary>
/// A wake-up signal between the outbox WRITERS (producers) and the <see cref="OutboxDispatcher"/>
/// (a single consumer). Without it the dispatcher only polls every couple of seconds, so a freshly
/// written event waits up to that long before it is published. With it, the write side rings a bell
/// the moment it stages rows and the dispatcher drains immediately — the poll stays only as a safety
/// net (in case a signal is ever missed or the broker was down).
///
/// Built on a <see cref="Channel{T}"/> used as a COALESCING signal: capacity 1 with
/// <see cref="BoundedChannelFullMode.DropWrite"/> means many <see cref="Notify"/> calls collapse into
/// at most one pending wake-up (we only need to know "there is work", not how many). Registered as a
/// singleton so producers and the consumer share the one channel.
/// </summary>
public interface IOutboxSignal
{
    /// <summary>Producer side: ring the bell (non-blocking; extra rings while one is pending are dropped).</summary>
    void Notify();

    /// <summary>
    /// Consumer side: wait until either a signal arrives or <paramref name="theMaxWait"/> elapses
    /// (the poll fallback), whichever comes first. Returns true if woken by a signal, false on timeout.
    /// </summary>
    Task<bool> WaitForWorkAsync(TimeSpan theMaxWait, CancellationToken theCancellationToken = default);
}

/// <inheritdoc cref="IOutboxSignal"/>
public sealed class OutboxSignal : IOutboxSignal
{
    // Capacity 1 + DropWrite = a coalescing "there is work" flag. A byte is just a token; its value is unused.
    private readonly Channel<byte> myChannel = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false, // several repositories (SQL + Mongo) may ring it concurrently
        });

    public void Notify() => myChannel.Writer.TryWrite(0);

    public async Task<bool> WaitForWorkAsync(TimeSpan theMaxWait, CancellationToken theCancellationToken = default)
    {
        using var aTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(theCancellationToken);
        aTimeoutCts.CancelAfter(theMaxWait);

        try
        {
            // Blocks (without holding a thread) until a signal is available or the timeout fires.
            await myChannel.Reader.ReadAsync(aTimeoutCts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!theCancellationToken.IsCancellationRequested)
        {
            // The timeout elapsed, not a real shutdown — fall through to a normal poll tick.
            return false;
        }
    }
}
