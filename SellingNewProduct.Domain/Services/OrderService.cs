using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
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

    public async Task<Order> CreateAsync(CreateOrderCommand theCommand, CancellationToken theCancellationToken = default)
    {
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

        var aShippingAddress = Address.Create(
            theCommand.Street, theCommand.Ward, theCommand.District, theCommand.City, theCommand.Country);

        var aOrder = Order.Create(theCommand.CustomerId, theCommand.EmployeeId, aShippingAddress);
        await myOrderRepository.AddAsync(aOrder, theCancellationToken);
        return aOrder;
    }

    public async Task<Order> AddDetailAsync(Guid theOrderId, Guid theProductId, int theQuantity, CancellationToken theCancellationToken = default)
    {
        var aOrder = await LoadOrderAsync(theOrderId, theCancellationToken);

        var aProduct = await myProductRepository.GetByIdAsync(theProductId, theCancellationToken);
        if (aProduct is null)
        {
            throw new NotFoundException($"Product '{theProductId}' not found.");
        }

        aOrder.AddDetail(aProduct, theQuantity);
        await myOrderRepository.UpdateAsync(aOrder, theCancellationToken);
        return aOrder;
    }

    public async Task<Order> ConfirmAsync(Guid theOrderId, CancellationToken theCancellationToken = default)
    {
        var aOrder = await LoadOrderAsync(theOrderId, theCancellationToken);
        aOrder.Confirm();
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
        aOrder.Cancel();
        await myOrderRepository.UpdateAsync(aOrder, theCancellationToken);
        return aOrder;
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
}
