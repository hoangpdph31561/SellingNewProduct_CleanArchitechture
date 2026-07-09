using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using SellingNewProduct.Infrastructure.Saga.Core.Resilience;

namespace SellingNewProduct.Infrastructure.Saga.Core.CrossDb;

/// <summary>
/// Resilience decorator for <see cref="ICrossDbDirectory"/>: runs every (idempotent) cross-store read
/// through the shared timeout + retry + circuit-breaker pipeline. When the breaker is OPEN or a call
/// times out, it degrades GRACEFULLY — an order read still renders with "(unknown)" names instead of
/// failing the whole request. Availability over completeness, the classic circuit-breaker trade.
/// The inner adapter and its MongoDB context are untouched (decorator pattern).
/// </summary>
public sealed class ResilientCrossDbDirectory : ICrossDbDirectory
{
    private static readonly IReadOnlyDictionary<Guid, string> EmptyNames = new Dictionary<Guid, string>();

    private readonly ICrossDbDirectory myInner;
    private readonly ResiliencePipeline myPipeline;
    private readonly ILogger<ResilientCrossDbDirectory> myLogger;

    public ResilientCrossDbDirectory(
        ICrossDbDirectory theInner, CrossStorePipeline thePipeline, ILogger<ResilientCrossDbDirectory> theLogger)
    {
        myInner = theInner;
        myPipeline = thePipeline.Pipeline;
        myLogger = theLogger;
    }

    public Task<string?> GetCustomerNameAsync(Guid theCustomerId, CancellationToken theCancellationToken = default) =>
        RunAsync(theCt => myInner.GetCustomerNameAsync(theCustomerId, theCt), null, nameof(GetCustomerNameAsync), theCancellationToken);

    public Task<IReadOnlyDictionary<Guid, string>> GetCustomerNamesAsync(CancellationToken theCancellationToken = default) =>
        RunAsync(myInner.GetCustomerNamesAsync, EmptyNames, nameof(GetCustomerNamesAsync), theCancellationToken);

    public Task<string?> GetEmployeeNameAsync(Guid theEmployeeId, CancellationToken theCancellationToken = default) =>
        RunAsync(theCt => myInner.GetEmployeeNameAsync(theEmployeeId, theCt), null, nameof(GetEmployeeNameAsync), theCancellationToken);

    public Task<IReadOnlyDictionary<Guid, string>> GetEmployeeNamesAsync(CancellationToken theCancellationToken = default) =>
        RunAsync(myInner.GetEmployeeNamesAsync, EmptyNames, nameof(GetEmployeeNamesAsync), theCancellationToken);

    public Task<IReadOnlyList<EmployeeInfo>> GetEmployeesAsync(CancellationToken theCancellationToken = default) =>
        RunAsync(myInner.GetEmployeesAsync, Array.Empty<EmployeeInfo>(), nameof(GetEmployeesAsync), theCancellationToken);

    public Task<IReadOnlyList<CatalogProduct>> GetProductsAsync(CancellationToken theCancellationToken = default) =>
        RunAsync(myInner.GetProductsAsync, Array.Empty<CatalogProduct>(), nameof(GetProductsAsync), theCancellationToken);

    public Task<IReadOnlyDictionary<Guid, string>> GetCategoryNamesAsync(CancellationToken theCancellationToken = default) =>
        RunAsync(myInner.GetCategoryNamesAsync, EmptyNames, nameof(GetCategoryNamesAsync), theCancellationToken);

    private async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> theOperation, T theFallback, string theName, CancellationToken theCancellationToken)
    {
        try
        {
            return await myPipeline.ExecuteAsync(async theCt => await theOperation(theCt), theCancellationToken);
        }
        catch (Exception aEx) when (aEx is BrokenCircuitException or TimeoutRejectedException)
        {
            myLogger.LogWarning("Cross-store '{Op}' short-circuited ({Reason}); returning fallback.", theName, aEx.GetType().Name);
            return theFallback;
        }
    }
}

/// <summary>
/// Resilience decorator for <see cref="ICrossDbOrderStats"/>: same pipeline, same graceful degradation
/// for the SQL-backed stats the MongoDB people read models stitch in. When the SQL side is unhealthy,
/// the breaker trips and stats fall back to zero/empty rather than failing the people read entirely.
/// </summary>
public sealed class ResilientCrossDbOrderStats : ICrossDbOrderStats
{
    private readonly ICrossDbOrderStats myInner;
    private readonly ResiliencePipeline myPipeline;
    private readonly ILogger<ResilientCrossDbOrderStats> myLogger;

    public ResilientCrossDbOrderStats(
        ICrossDbOrderStats theInner, CrossStorePipeline thePipeline, ILogger<ResilientCrossDbOrderStats> theLogger)
    {
        myInner = theInner;
        myPipeline = thePipeline.Pipeline;
        myLogger = theLogger;
    }

    public Task<int> CountSalesOrdersForEmployeeAsync(Guid theEmployeeId, CancellationToken theCancellationToken = default) =>
        RunAsync(theCt => myInner.CountSalesOrdersForEmployeeAsync(theEmployeeId, theCt), 0, nameof(CountSalesOrdersForEmployeeAsync), theCancellationToken);

    public Task<IReadOnlyDictionary<Guid, int>> CountSalesOrdersByEmployeesAsync(
        IReadOnlyCollection<Guid> theEmployeeIds, CancellationToken theCancellationToken = default) =>
        RunAsync(theCt => myInner.CountSalesOrdersByEmployeesAsync(theEmployeeIds, theCt),
            new Dictionary<Guid, int>(), nameof(CountSalesOrdersByEmployeesAsync), theCancellationToken);

    public Task<IReadOnlyList<CustomerOrderTotal>> GetCustomerOrderTotalsAsync(CancellationToken theCancellationToken = default) =>
        RunAsync(myInner.GetCustomerOrderTotalsAsync, Array.Empty<CustomerOrderTotal>(), nameof(GetCustomerOrderTotalsAsync), theCancellationToken);

    private async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> theOperation, T theFallback, string theName, CancellationToken theCancellationToken)
    {
        try
        {
            return await myPipeline.ExecuteAsync(async theCt => await theOperation(theCt), theCancellationToken);
        }
        catch (Exception aEx) when (aEx is BrokenCircuitException or TimeoutRejectedException)
        {
            myLogger.LogWarning("Cross-store '{Op}' short-circuited ({Reason}); returning fallback.", theName, aEx.GetType().Name);
            return theFallback;
        }
    }
}
