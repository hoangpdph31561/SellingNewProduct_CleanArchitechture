using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Services;

internal sealed class OrderService : IOrderService
{
    private readonly IOrderRepository myOrderRepository;
    private readonly ICustomerRepository myCustomerRepository;
    private readonly IEmployeeRepository myEmployeeRepository;
    private readonly IProductRepository myProductRepository;

    public OrderService(
        IOrderRepository theOrderRepository,
        ICustomerRepository theCustomerRepository,
        IEmployeeRepository theEmployeeRepository,
        IProductRepository theProductRepository)
    {
        myOrderRepository = theOrderRepository;
        myCustomerRepository = theCustomerRepository;
        myEmployeeRepository = theEmployeeRepository;
        myProductRepository = theProductRepository;
    }

    public Task<Order?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myOrderRepository.GetByIdAsync(theId, theCancellationToken);

    public async Task<Order> PlaceAsync(PlaceOrderCommand theCommand, CancellationToken theCancellationToken = default)
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

            EnsureProductIsActive(aProduct);
            EnsureEnoughStock(aProduct, aItem.Quantity);

            aOrder.AddDetail(aProduct, aItem.Quantity);
        }

        await myOrderRepository.AddAsync(aOrder, theCancellationToken);
        return aOrder;
    }

    public async Task<Order> ConfirmAsync(Guid theOrderId, CancellationToken theCancellationToken = default)
    {
        var aOrder = await LoadOrderAsync(theOrderId, theCancellationToken);

        // Reserve stock: re-check availability now (it may have changed since the order was placed),
        // then decrement each product. Order is confirmed only after the stock check passes.
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

        // NOTE: in a real system the two SaveChanges below would share one transaction (Unit of Work).
        await myProductRepository.UpdateRangeAsync(aProducts.Values, theCancellationToken);
        await myOrderRepository.UpdateAsync(aOrder, theCancellationToken);
        return aOrder;
    }

    public async Task<Order> ShipAsync(Guid theOrderId, CancellationToken theCancellationToken = default)
    {
        var aOrder = await LoadOrderAsync(theOrderId, theCancellationToken);
        aOrder.MarkShipped();
        await myOrderRepository.UpdateAsync(aOrder, theCancellationToken);
        return aOrder;
    }

    public async Task<Order> CancelAsync(Guid theOrderId, CancellationToken theCancellationToken = default)
    {
        var aOrder = await LoadOrderAsync(theOrderId, theCancellationToken);

        // Stock was only reserved once the order was Confirmed, so only a confirmed order returns it.
        var aWasConfirmed = aOrder.OrderStatus == OrderStatus.Confirmed;

        aOrder.Cancel();

        if (aWasConfirmed)
        {
            var aProducts = await LoadOrderProductsAsync(aOrder, theCancellationToken);

            foreach (var aDetail in aOrder.Details)
            {
                if (aProducts.TryGetValue(aDetail.ProductId, out var aProduct))
                {
                    aProduct.IncreaseStock(aDetail.Quantity);
                }
            }

            await myProductRepository.UpdateRangeAsync(aProducts.Values, theCancellationToken);
        }

        await myOrderRepository.UpdateAsync(aOrder, theCancellationToken);
        return aOrder;
    }

    public Task<OrderDetailView?> GetOrderDetailAsync(Guid theOrderId, CancellationToken theCancellationToken = default)
        => myOrderRepository.GetOrderDetailAsync(theOrderId, theCancellationToken);

    public Task<CustomerOrderHistoryView?> GetCustomerHistoryAsync(Guid theCustomerId, CancellationToken theCancellationToken = default)
        => myOrderRepository.GetCustomerHistoryAsync(theCustomerId, theCancellationToken);

    public Task<PagedResult<OrderSummaryView>> SearchAsync(OrderSearchQuery theQuery, CancellationToken theCancellationToken = default)
        => myOrderRepository.SearchAsync(theQuery, theCancellationToken);

    public Task<IReadOnlyList<OrderStatusCountView>> GetStatusBreakdownAsync(CancellationToken theCancellationToken = default)
        => myOrderRepository.GetStatusBreakdownAsync(theCancellationToken);

    private async Task<Order> LoadOrderAsync(Guid theOrderId, CancellationToken theCancellationToken)
    {
        var aOrder = await myOrderRepository.GetByIdAsync(theOrderId, theCancellationToken);
        if (aOrder is null)
        {
            throw new NotFoundException($"Order '{theOrderId}' not found.");
        }

        return aOrder;
    }

    // Loads the products referenced by an order's lines, keyed by id, for stock adjustments.
    private async Task<Dictionary<Guid, Product>> LoadOrderProductsAsync(Order theOrder, CancellationToken theCancellationToken)
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
