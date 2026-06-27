namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Saga;

/// <summary>
/// One product's stock delta a saga removed (negative if it added). Serialized as the
/// <c>CompensationData</c> of the stock step so the compensation can add exactly that amount back.
/// </summary>
internal sealed record StockDelta(Guid ProductId, int Removed);
