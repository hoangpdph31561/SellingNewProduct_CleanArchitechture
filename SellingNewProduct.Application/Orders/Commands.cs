using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Application.Orders;

// ----- Place order --------------------------------------------------------------------------

/// <summary>Write-side command: place a complete order in one call. Created as Draft.</summary>
public sealed record PlaceOrderCommand(
    Guid CustomerId,
    Guid EmployeeId,
    string Street,
    string Ward,
    string District,
    string City,
    string Country,
    IReadOnlyList<OrderItemCommand> Items) : IRequest<Order>;

/// <summary>One requested line of a <see cref="PlaceOrderCommand"/>.</summary>
public sealed record OrderItemCommand(Guid ProductId, int Quantity);

public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Street).NotEmpty().MaximumLength(300);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.ProductId).NotEmpty();
            i.RuleFor(x => x.Quantity).GreaterThan(0);
        });
    }
}

public sealed class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Order>
{
    private readonly IOrderWriteRepository myOrderRepository;
    private readonly ICustomerWriteRepository myCustomerRepository;
    private readonly IEmployeeWriteRepository myEmployeeRepository;
    private readonly IProductWriteRepository myProductRepository;

    public PlaceOrderCommandHandler(
        IOrderWriteRepository theOrderRepository,
        ICustomerWriteRepository theCustomerRepository,
        IEmployeeWriteRepository theEmployeeRepository,
        IProductWriteRepository theProductRepository)
    {
        myOrderRepository = theOrderRepository;
        myCustomerRepository = theCustomerRepository;
        myEmployeeRepository = theEmployeeRepository;
        myProductRepository = theProductRepository;
    }

    public async Task<Order> Handle(PlaceOrderCommand theCommand, CancellationToken theCancellationToken)
    {
        if (theCommand.Items is null || theCommand.Items.Count == 0)
        {
            throw new DomainException("An order must contain at least one item.");
        }

        // The order must reference an existing customer and employee.
        var aCustomer = await myCustomerRepository.GetByIdAsync(theCommand.CustomerId, theCancellationToken);
        if (aCustomer is null)
        {
            throw new NotFoundException($"Customer '{theCommand.CustomerId}' not found.");
        }

        var aEmployee = await myEmployeeRepository.GetByIdAsync(theCommand.EmployeeId, theCancellationToken);
        if (aEmployee is null)
        {
            throw new NotFoundException($"Employee '{theCommand.EmployeeId}' not found.");
        }

        // Load every referenced product once, then add each line — validating availability and
        // stock as we go, so the whole order is rejected if any line is invalid.
        var aProductIds = theCommand.Items.Select(i => i.ProductId).Distinct().ToList();
        var aProductById = (await myProductRepository.GetByIdsAsync(aProductIds, theCancellationToken))
            .ToDictionary(p => p.Id);

        var aShippingAddress = Address.Create(
            theCommand.Street, theCommand.Ward, theCommand.District, theCommand.City, theCommand.Country);

        var aOrder = Order.Create(theCommand.CustomerId, theCommand.EmployeeId, aShippingAddress);

        foreach (var aItem in theCommand.Items)
        {
            if (!aProductById.TryGetValue(aItem.ProductId, out var aProduct))
            {
                throw new NotFoundException($"Product '{aItem.ProductId}' not found.");
            }

            OrderRules.EnsureProductIsActive(aProduct);
            OrderRules.EnsureEnoughStock(aProduct, aItem.Quantity);

            aOrder.AddDetail(aProduct, aItem.Quantity);
        }

        await myOrderRepository.AddAsync(aOrder, theCancellationToken);
        return aOrder;
    }
}

// ----- Confirm order (TRANSACTIONAL: writes Products + Orders atomically) -------------------

public sealed record ConfirmOrderCommand(Guid Id) : IRequest<Order>;

public sealed class ConfirmOrderCommandHandler : IRequestHandler<ConfirmOrderCommand, Order>
{
    private readonly IOrderWriteRepository myOrderRepository;
    private readonly IProductWriteRepository myProductRepository;
    private readonly IUnitOfWork myUnitOfWork;

    public ConfirmOrderCommandHandler(
        IOrderWriteRepository theOrderRepository,
        IProductWriteRepository theProductRepository,
        IUnitOfWork theUnitOfWork)
    {
        myOrderRepository = theOrderRepository;
        myProductRepository = theProductRepository;
        myUnitOfWork = theUnitOfWork;
    }

    public async Task<Order> Handle(ConfirmOrderCommand theCommand, CancellationToken theCancellationToken)
    {
        var aOrder = await OrderRules.LoadOrderAsync(myOrderRepository, theCommand.Id, theCancellationToken);

        // Reserve stock: re-check availability now (it may have changed since the order was placed),
        // then decrement each product. Order is confirmed only after the stock check passes.
        var aProducts = await OrderRules.LoadOrderProductsAsync(myProductRepository, aOrder, theCancellationToken);

        foreach (var aDetail in aOrder.Details)
        {
            var aProduct = OrderRules.GetProductOrThrow(aProducts, aDetail.ProductId);
            OrderRules.EnsureEnoughStock(aProduct, aDetail.Quantity);
        }

        aOrder.Confirm();

        foreach (var aDetail in aOrder.Details)
        {
            aProducts[aDetail.ProductId].DecreaseStock(aDetail.Quantity);
        }

        // The two writes below (stock down + order confirmed) must be all-or-nothing.
        // On MongoDB this requires a replica set; on a standalone server it will fail here.
        await using var aTransaction = await myUnitOfWork.BeginTransactionAsync(theCancellationToken);
        try
        {
            await myProductRepository.UpdateRangeAsync(aProducts.Values, theCancellationToken);
            await myOrderRepository.UpdateAsync(aOrder, theCancellationToken);
            await aTransaction.CommitAsync(theCancellationToken);
        }
        catch
        {
            await aTransaction.RollbackAsync(theCancellationToken);
            throw;
        }

        return aOrder;
    }
}

// ----- Ship order ---------------------------------------------------------------------------

public sealed record ShipOrderCommand(Guid Id) : IRequest<Order>;

public sealed class ShipOrderCommandHandler : IRequestHandler<ShipOrderCommand, Order>
{
    private readonly IOrderWriteRepository myOrderRepository;

    public ShipOrderCommandHandler(IOrderWriteRepository theOrderRepository)
    {
        myOrderRepository = theOrderRepository;
    }

    public async Task<Order> Handle(ShipOrderCommand theCommand, CancellationToken theCancellationToken)
    {
        var aOrder = await OrderRules.LoadOrderAsync(myOrderRepository, theCommand.Id, theCancellationToken);
        aOrder.MarkShipped();
        await myOrderRepository.UpdateAsync(aOrder, theCancellationToken);
        return aOrder;
    }
}

// ----- Cancel order (TRANSACTIONAL when stock must be returned) -----------------------------

public sealed record CancelOrderCommand(Guid Id) : IRequest<Order>;

public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, Order>
{
    private readonly IOrderWriteRepository myOrderRepository;
    private readonly IProductWriteRepository myProductRepository;
    private readonly IUnitOfWork myUnitOfWork;

    public CancelOrderCommandHandler(
        IOrderWriteRepository theOrderRepository,
        IProductWriteRepository theProductRepository,
        IUnitOfWork theUnitOfWork)
    {
        myOrderRepository = theOrderRepository;
        myProductRepository = theProductRepository;
        myUnitOfWork = theUnitOfWork;
    }

    public async Task<Order> Handle(CancelOrderCommand theCommand, CancellationToken theCancellationToken)
    {
        var aOrder = await OrderRules.LoadOrderAsync(myOrderRepository, theCommand.Id, theCancellationToken);

        // Stock was only reserved once the order was Confirmed, so only a confirmed order returns it.
        var aWasConfirmed = aOrder.OrderStatus == OrderStatus.Confirmed;

        aOrder.Cancel();

        if (!aWasConfirmed)
        {
            // Nothing to return — a single write, no transaction needed.
            await myOrderRepository.UpdateAsync(aOrder, theCancellationToken);
            return aOrder;
        }

        var aProducts = await OrderRules.LoadOrderProductsAsync(myProductRepository, aOrder, theCancellationToken);

        foreach (var aDetail in aOrder.Details)
        {
            if (aProducts.TryGetValue(aDetail.ProductId, out var aProduct))
            {
                aProduct.IncreaseStock(aDetail.Quantity);
            }
        }

        await using var aTransaction = await myUnitOfWork.BeginTransactionAsync(theCancellationToken);
        try
        {
            await myProductRepository.UpdateRangeAsync(aProducts.Values, theCancellationToken);
            await myOrderRepository.UpdateAsync(aOrder, theCancellationToken);
            await aTransaction.CommitAsync(theCancellationToken);
        }
        catch
        {
            await aTransaction.RollbackAsync(theCancellationToken);
            throw;
        }

        return aOrder;
    }
}

// ----- Shared order-handler helpers ---------------------------------------------------------

internal static class OrderRules
{
    public static async Task<Order> LoadOrderAsync(IOrderWriteRepository theRepository, Guid theOrderId, CancellationToken theCancellationToken)
    {
        var aOrder = await theRepository.GetByIdAsync(theOrderId, theCancellationToken);
        if (aOrder is null)
        {
            throw new NotFoundException($"Order '{theOrderId}' not found.");
        }

        return aOrder;
    }

    public static async Task<Dictionary<Guid, Product>> LoadOrderProductsAsync(
        IProductWriteRepository theRepository, Order theOrder, CancellationToken theCancellationToken)
    {
        var aProductIds = theOrder.Details.Select(d => d.ProductId).Distinct().ToList();
        var aProducts = await theRepository.GetByIdsAsync(aProductIds, theCancellationToken);
        return aProducts.ToDictionary(p => p.Id);
    }

    public static Product GetProductOrThrow(IReadOnlyDictionary<Guid, Product> theProducts, Guid theProductId)
    {
        if (!theProducts.TryGetValue(theProductId, out var aProduct))
        {
            throw new NotFoundException($"Product '{theProductId}' referenced by the order no longer exists.");
        }

        return aProduct;
    }

    public static void EnsureProductIsActive(Product theProduct)
    {
        if (theProduct.Status != EntityStatus.Active)
        {
            throw new ConflictException($"Product '{theProduct.Name}' is not available for ordering.");
        }
    }

    public static void EnsureEnoughStock(Product theProduct, int theQuantity)
    {
        if (theQuantity > theProduct.StockQuantity)
        {
            throw new ConflictException(
                $"Not enough stock for product '{theProduct.Name}'. Available: {theProduct.StockQuantity}, requested: {theQuantity}.");
        }
    }
}
