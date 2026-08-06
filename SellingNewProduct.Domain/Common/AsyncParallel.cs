namespace SellingNewProduct.Domain.Common;

/// <summary>
/// Small helpers for running independent work concurrently with a BOUNDED degree of parallelism.
/// "Bounded" matters: firing thousands of tasks at once would exhaust the thread pool, database
/// connections or a downstream service's limits, so a <see cref="SemaphoreSlim"/> caps how many
/// run at the same time (the classic async throttle).
///
/// IMPORTANT — only use these for work that is genuinely independent AND touches no shared,
/// non-thread-safe resource. An EF Core <c>DbContext</c> is NOT thread-safe: never fan out several
/// queries that share one context. Batch those into a single round-trip, or give each branch its
/// own context. Good targets here are CPU-bound mapping and calls to thread-safe external clients
/// (SMTP, message brokers, HTTP).
/// </summary>
public static class AsyncParallel
{
    /// <summary>
    /// Projects each item through <paramref name="theBody"/> concurrently, running at most
    /// <paramref name="theMaxDegreeOfParallelism"/> at a time, and returns the results in the
    /// SAME order as the input.
    /// </summary>
    public static async Task<IReadOnlyList<TResult>> ForEachAsync<TSource, TResult>(
        IEnumerable<TSource> theSource,
        int theMaxDegreeOfParallelism,
        Func<TSource, CancellationToken, Task<TResult>> theBody,
        CancellationToken theCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(theSource);
        ArgumentNullException.ThrowIfNull(theBody);
        if (theMaxDegreeOfParallelism < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(theMaxDegreeOfParallelism));
        }

        using var aThrottle = new SemaphoreSlim(theMaxDegreeOfParallelism, theMaxDegreeOfParallelism);

        // One task per item. Each waits for a free slot on the semaphore before it starts real work,
        // so no more than theMaxDegreeOfParallelism bodies run at once. Task.WhenAll returns the
        // results as an array in task order, which is the input order.
        var aTasks = theSource.Select(async aItem =>
        {
            await aThrottle.WaitAsync(theCancellationToken);
            try
            {
                return await theBody(aItem, theCancellationToken);
            }
            finally
            {
                // Always release, even on failure, or the throttle would leak slots and deadlock.
                aThrottle.Release();
            }
        }).ToList();

        return await Task.WhenAll(aTasks);
    }

    /// <summary>Runs a side-effecting body over each item with bounded parallelism (no result).</summary>
    public static Task ForEachAsync<TSource>(
        IEnumerable<TSource> theSource,
        int theMaxDegreeOfParallelism,
        Func<TSource, CancellationToken, Task> theBody,
        CancellationToken theCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(theBody);

        return ForEachAsync(theSource, theMaxDegreeOfParallelism, async (aItem, aCancellationToken) =>
        {
            await theBody(aItem, aCancellationToken);
            return true; // dummy result so we can reuse the projecting overload
        }, theCancellationToken);
    }
}
