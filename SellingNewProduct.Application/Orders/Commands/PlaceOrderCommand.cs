using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Application.Orders;

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

public sealed class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Order>
{
    private readonly IOrderWriteService myOrderWriteService;

    public PlaceOrderCommandHandler(IOrderWriteService theOrderWriteService)
    {
        myOrderWriteService = theOrderWriteService;
    }

    public Task<Order> Handle(PlaceOrderCommand theCommand, CancellationToken theCancellationToken)
    {
        var aShippingAddress = Address.Create(
            theCommand.Street, theCommand.Ward, theCommand.District, theCommand.City, theCommand.Country);

        var aLines = theCommand.Items
            .Select(i => new OrderLine(i.ProductId, i.Quantity))
            .ToList();

        return myOrderWriteService.PlaceAsync(
            theCommand.CustomerId, theCommand.EmployeeId, aShippingAddress, aLines, theCancellationToken);
    }
}

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
