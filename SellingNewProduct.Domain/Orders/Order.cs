using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Orders;

/// <summary>
/// An order. Aggregate root that owns its <see cref="OrderDetail"/> lines.
/// All changes to the lines go through this root, which guards the invariants.
/// References Customer/Employee by id only.
/// </summary>
public sealed class Order : AggregateRoot<Guid>
{
    private readonly List<OrderDetail> myListDetails = new();

    public Guid CustomerId { get; private set; }

    public Guid EmployeeId { get; private set; }

    public OrderStatus OrderStatus { get; private set; }

    public DateTime OrderDate { get; private set; }

    public Address ShippingAddress { get; private set; } = default!;

    public IReadOnlyList<OrderDetail> Details => myListDetails.AsReadOnly();

    public Money TotalAmount { get; private set; } = Money.Zero();

    private Order()
    {
    }

    private Order(Guid theId, Guid theCustomerId, Guid theEmployeeId, Address theShippingAddress)
        : base(theId)
    {
        CustomerId = theCustomerId;
        EmployeeId = theEmployeeId;
        ShippingAddress = theShippingAddress;
        OrderStatus = OrderStatus.Draft;
        OrderDate = DateTime.UtcNow;
        TotalAmount = Money.Zero();
    }

    public static Order Create(Guid theCustomerId, Guid theEmployeeId, Address theShippingAddress)
    {
        if (theCustomerId == Guid.Empty)
        {
            throw new DomainException("Order must reference a customer.");
        }

        if (theEmployeeId == Guid.Empty)
        {
            throw new DomainException("Order must reference an employee.");
        }

        return new Order(Guid.NewGuid(), theCustomerId, theEmployeeId, theShippingAddress);
    }

    public void AddDetail(Product theProduct, int theQuantity)
    {
        EnsureDraft();

        if (theQuantity <= 0)
        {
            throw new DomainException("Quantity must be positive.");
        }

        var aExisting = myListDetails.SingleOrDefault(d => d.ProductId == theProduct.Id);

        if (aExisting is not null)
        {
            aExisting.IncreaseQuantity(theQuantity);
        }
        else
        {
            myListDetails.Add(OrderDetail.Create(theProduct.Id, theProduct.Name, theProduct.Price, theQuantity));
        }

        Recalculate();
    }

    public void ChangeDetailQuantity(Guid theOrderDetailId, int theNewQuantity)
    {
        EnsureDraft();

        var aDetail = FindDetail(theOrderDetailId);
        aDetail.ChangeQuantity(theNewQuantity);
        Recalculate();
    }

    public void RemoveDetail(Guid theOrderDetailId)
    {
        EnsureDraft();

        var aDetail = FindDetail(theOrderDetailId);
        myListDetails.Remove(aDetail);
        Recalculate();
    }

    public void Confirm()
    {
        EnsureDraft();

        if (myListDetails.Count == 0)
        {
            throw new DomainException("Cannot confirm an order with no items.");
        }

        OrderStatus = OrderStatus.Confirmed;
        MarkUpdated();
        Raise(new OrderConfirmedEvent(Id, CustomerId, TotalAmount.Amount, TotalAmount.Currency));
    }

    /// <summary>
    /// Signals that the order has been fully placed (created with all its lines). Raised by the
    /// domain service once the details are attached, so the event carries the real total.
    /// </summary>
    public void MarkPlaced()
    {
        Raise(new OrderPlacedEvent(Id, CustomerId, EmployeeId, TotalAmount.Amount, TotalAmount.Currency));
    }

    public void MarkShipped()
    {
        if (OrderStatus != OrderStatus.Confirmed)
        {
            throw new DomainException("Only a confirmed order can be shipped.");
        }

        OrderStatus = OrderStatus.Shipped;
        MarkUpdated();
        Raise(new OrderShippedEvent(Id, CustomerId));
    }

    public void Cancel()
    {
        if (OrderStatus == OrderStatus.Shipped)
        {
            throw new DomainException("A shipped order cannot be cancelled.");
        }

        if (OrderStatus == OrderStatus.Cancelled)
        {
            throw new DomainException("Order is already cancelled.");
        }

        var aWasConfirmed = OrderStatus == OrderStatus.Confirmed;

        OrderStatus = OrderStatus.Cancelled;
        MarkUpdated();
        Raise(new OrderCancelledEvent(Id, CustomerId, aWasConfirmed));
    }

    private void EnsureDraft()
    {
        if (OrderStatus != OrderStatus.Draft)
        {
            throw new DomainException($"Order can only be modified while in Draft. Current status: {OrderStatus}.");
        }
    }

    private OrderDetail FindDetail(Guid theOrderDetailId)
    {
        var aDetail = myListDetails.SingleOrDefault(d => d.Id == theOrderDetailId);

        if (aDetail is null)
        {
            throw new DomainException("Order detail not found in this order.");
        }

        return aDetail;
    }

    private void Recalculate()
    {
        var aTotal = Money.Zero();

        foreach (var aDetail in myListDetails)
        {
            aTotal = aTotal.Add(aDetail.LineTotal);
        }

        TotalAmount = aTotal;
        MarkUpdated();
    }

    public static Order Rehydrate(
        Guid theId,
        Guid theCustomerId,
        Guid theEmployeeId,
        OrderStatus theOrderStatus,
        DateTime theOrderDate,
        Address theShippingAddress,
        Money theTotalAmount,
        IEnumerable<OrderDetail> theDetails,
        EntityStatus theStatus,
        DateTime theCreatedAtUtc,
        DateTime? theUpdatedAtUtc)
    {
        var aOrder = new Order
        {
            CustomerId = theCustomerId,
            EmployeeId = theEmployeeId,
            OrderStatus = theOrderStatus,
            OrderDate = theOrderDate,
            ShippingAddress = theShippingAddress,
            TotalAmount = theTotalAmount
        };

        aOrder.myListDetails.AddRange(theDetails);
        aOrder.RestoreState(theId, theStatus, theCreatedAtUtc, theUpdatedAtUtc);
        return aOrder;
    }
}
