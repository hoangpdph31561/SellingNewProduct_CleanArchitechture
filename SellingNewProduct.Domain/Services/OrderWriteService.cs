using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Services;

/// <summary>
/// Implements the order inbound port. It owns the business rules that span more than one
/// aggregate (Order together with Product stock) and drives the outbound ports (repositories,
/// unit of work) to load and persist them. Depends only on Domain abstractions — never on
/// MediatR or any infrastructure type, so the core stays pure.
/// </summary>
public sealed class OrderWriteService : IOrderWriteService
{
    private readonly IOrderWriteRepository myOrderRepository;
    private readonly ICustomerWriteRepository myCustomerRepository;
    private readonly IEmployeeWriteRepository myEmployeeRepository;
    private readonly IProductWriteRepository myProductRepository;
    private readonly IUnitOfWork myUnitOfWork;

    public OrderWriteService(
        IOrderWriteRepository theOrderRepository,
        ICustomerWriteRepository theCustomerRepository,
        IEmployeeWriteRepository theEmployeeRepository,
        IProductWriteRepository theProductRepository,
        IUnitOfWork theUnitOfWork)
    {
        myOrderRepository = theOrderRepository;
        myCustomerRepository = theCustomerRepository;
        myEmployeeRepository = theEmployeeRepository;
        myProductRepository = theProductRepository;
        myUnitOfWork = theUnitOfWork;
    }

    /// <summary>Places a complete order in one call. The order is created as Draft.</summary>
    public async Task<Order> PlaceAsync(
        Guid theCustomerId,
        Guid theEmployeeId,
        Address theShippingAddress,
        IReadOnlyList<OrderLine> theLines,
        CancellationToken theCancellationToken = default)
    {
        if (theLines is null || theLines.Count == 0)
        {
            throw new DomainException("An order must contain at least one item.");
        }

        // The order must reference an existing customer and employee.
        if (await myCustomerRepository.GetByIdAsync(theCustomerId, theCancellationToken) is null)
        {
            throw new NotFoundException($"Customer '{theCustomerId}' not found.");
        }

        if (await myEmployeeRepository.GetByIdAsync(theEmployeeId, theCancellationToken) is null)
        {
            throw new NotFoundException($"Employee '{theEmployeeId}' not found.");
        }

        // Load every referenced product once, then add each line — validating availability and
        // stock as we go, so the whole order is rejected if any line is invalid.
        var aProductIds = theLines.Select(l => l.ProductId).Distinct().ToList();
        var aProductById = (await myProductRepository.GetByIdsAsync(aProductIds, theCancellationToken))
            .ToDictionary(p => p.Id);

        var aOrder = Order.Create(theCustomerId, theEmployeeId, theShippingAddress);

        foreach (var aLine in theLines)
        {
            var aProduct = GetProductOrThrow(aProductById, aLine.ProductId);
            EnsureProductIsActive(aProduct);
            EnsureEnoughStock(aProduct, aLine.Quantity);
            aOrder.AddDetail(aProduct, aLine.Quantity);
        }

        await myOrderRepository.AddAsync(aOrder, theCancellationToken);
        return aOrder;
    }

    /// <summary>
    /// Confirms a draft order and reserves stock. Re-checks availability (it may have changed
    /// since the order was placed), then decrements each product. The stock decrement and the
    /// order update are committed atomically.
    /// </summary>
    public async Task<Order> ConfirmAsync(Guid theOrderId, CancellationToken theCancellationToken = default)
    {
        var aOrder = await LoadOrderAsync(theOrderId, theCancellationToken);
        var aProducts = await LoadOrderProductsAsync(aOrder, theCancellationToken);

        foreach (var aDetail in aOrder.Details)
        {
            var aProduct = GetProductOrThrow(aProducts, aDetail.ProductId);
            EnsureEnoughStock(aProduct, aDetail.Quantity);
        }

        aOrder.Confirm();

        foreach (var aDetail in aOrder.Details)
        {
            aProducts[aDetail.ProductId].DecreaseStock(aDetail.Quantity);
        }

        // The two writes below (stock down + order confirmed) must be all-or-nothing.
        // On MongoDB this requires a replica set; on a standalone server it will fail here.
        await PersistInTransactionAsync(aOrder, aProducts.Values, theCancellationToken);
        return aOrder;
    }

    /// <summary>Marks a confirmed order as shipped.</summary>
    public async Task<Order> ShipAsync(Guid theOrderId, CancellationToken theCancellationToken = default)
    {
        var aOrder = await LoadOrderAsync(theOrderId, theCancellationToken);
        aOrder.MarkShipped();
        await myOrderRepository.UpdateAsync(aOrder, theCancellationToken);
        return aOrder;
    }

    /// <summary>
    /// Cancels an order. Stock was only reserved once the order was Confirmed, so a confirmed
    /// order returns its stock (atomically); any earlier-stage order is a single write.
    /// </summary>
    public async Task<Order> CancelAsync(Guid theOrderId, CancellationToken theCancellationToken = default)
    {
        var aOrder = await LoadOrderAsync(theOrderId, theCancellationToken);

        var aWasConfirmed = aOrder.OrderStatus == OrderStatus.Confirmed;

        aOrder.Cancel();

        if (!aWasConfirmed)
        {
            // Nothing to return — a single write, no transaction needed.
            await myOrderRepository.UpdateAsync(aOrder, theCancellationToken);
            return aOrder;
        }

        var aProducts = await LoadOrderProductsAsync(aOrder, theCancellationToken);

        foreach (var aDetail in aOrder.Details)
        {
            if (aProducts.TryGetValue(aDetail.ProductId, out var aProduct))
            {
                aProduct.IncreaseStock(aDetail.Quantity);
            }
        }

        await PersistInTransactionAsync(aOrder, aProducts.Values, theCancellationToken);
        return aOrder;
    }

    // ----- Persistence + lookup helpers ---------------------------------------------------------

    private async Task PersistInTransactionAsync(
        Order theOrder, IEnumerable<Product> theProducts, CancellationToken theCancellationToken)
    {
        await using var aTransaction = await myUnitOfWork.BeginTransactionAsync(theCancellationToken);
        try
        {
            await myProductRepository.UpdateRangeAsync(theProducts, theCancellationToken);
            await myOrderRepository.UpdateAsync(theOrder, theCancellationToken);
            await aTransaction.CommitAsync(theCancellationToken);
        }
        catch
        {
            await aTransaction.RollbackAsync(theCancellationToken);
            throw;
        }
    }

    private async Task<Order> LoadOrderAsync(Guid theOrderId, CancellationToken theCancellationToken)
    {
        var aOrder = await myOrderRepository.GetByIdAsync(theOrderId, theCancellationToken);
        if (aOrder is null)
        {
            throw new NotFoundException($"Order '{theOrderId}' not found.");
        }

        return aOrder;
    }

    private async Task<Dictionary<Guid, Product>> LoadOrderProductsAsync(
        Order theOrder, CancellationToken theCancellationToken)
    {
        var aProductIds = theOrder.Details.Select(d => d.ProductId).Distinct().ToList();
        var aProducts = await myProductRepository.GetByIdsAsync(aProductIds, theCancellationToken);
        return aProducts.ToDictionary(p => p.Id);
    }

    private static Product GetProductOrThrow(IReadOnlyDictionary<Guid, Product> theProducts, Guid theProductId)
    {
        if (!theProducts.TryGetValue(theProductId, out var aProduct))
        {
            throw new NotFoundException($"Product '{theProductId}' referenced by the order no longer exists.");
        }

        return aProduct;
    }

    private static void EnsureProductIsActive(Product theProduct)
    {
        if (theProduct.Status != EntityStatus.Active)
        {
            throw new ConflictException($"Product '{theProduct.Name}' is not available for ordering.");
        }
    }

    private static void EnsureEnoughStock(Product theProduct, int theQuantity)
    {
        if (theQuantity > theProduct.StockQuantity)
        {
            throw new ConflictException(
                $"Not enough stock for product '{theProduct.Name}'. Available: {theProduct.StockQuantity}, requested: {theQuantity}.");
        }
    }
}
