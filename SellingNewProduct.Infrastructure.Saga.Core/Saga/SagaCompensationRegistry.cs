namespace SellingNewProduct.Infrastructure.Saga.Core.Saga;

/// <summary>
/// Default registry: indexes every <see cref="ISagaCompensationHandler"/> registered in DI by its
/// <see cref="ISagaCompensationHandler.CompensationType"/>. Each database project contributes its
/// own handlers (Mongo stock, future SQL/email handlers, ...) and they all land here automatically.
/// </summary>
internal sealed class SagaCompensationRegistry : ISagaCompensationRegistry
{
    private readonly IReadOnlyDictionary<string, ISagaCompensationHandler> myHandlers;

    public SagaCompensationRegistry(IEnumerable<ISagaCompensationHandler> theHandlers)
    {
        // Last registration wins if two handlers claim the same type — keeps composition predictable.
        var aMap = new Dictionary<string, ISagaCompensationHandler>(StringComparer.Ordinal);
        foreach (var aHandler in theHandlers)
        {
            aMap[aHandler.CompensationType] = aHandler;
        }

        myHandlers = aMap;
    }

    public ISagaCompensationHandler? Resolve(string theCompensationType)
        => myHandlers.TryGetValue(theCompensationType, out var aHandler) ? aHandler : null;
}
