using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.Employees;

/// <summary>
/// A staff member who creates orders. Aggregate root. Linked to a login
/// account through <see cref="UserId"/> (referenced by id).
/// </summary>
public sealed class Employee : AggregateRoot<Guid>
{
    public string FullName { get; private set; } = default!;

    public string Position { get; private set; } = default!;

    public DateTime HireDate { get; private set; }

    public Guid UserId { get; private set; }

    private Employee()
    {
    }

    private Employee(Guid theId, string theFullName, string thePosition, DateTime theHireDate, Guid theUserId)
        : base(theId)
    {
        FullName = theFullName;
        Position = thePosition;
        HireDate = theHireDate;
        UserId = theUserId;
    }

    public static Employee Create(string theFullName, string thePosition, DateTime theHireDate, Guid theUserId)
    {
        if (string.IsNullOrWhiteSpace(theFullName))
        {
            throw new DomainException("Employee full name is required.");
        }

        if (string.IsNullOrWhiteSpace(thePosition))
        {
            throw new DomainException("Employee position is required.");
        }

        if (theUserId == Guid.Empty)
        {
            throw new DomainException("Employee must be linked to a user account.");
        }

        return new Employee(Guid.NewGuid(), theFullName.Trim(), thePosition.Trim(), theHireDate, theUserId);
    }

    public void ChangePosition(string theNewPosition)
    {
        if (string.IsNullOrWhiteSpace(theNewPosition))
        {
            throw new DomainException("Employee position is required.");
        }

        Position = theNewPosition.Trim();
        MarkUpdated();
    }

    public static Employee Rehydrate(
        Guid theId,
        string theFullName,
        string thePosition,
        DateTime theHireDate,
        Guid theUserId,
        EntityStatus theStatus,
        DateTime theCreatedAtUtc,
        DateTime? theUpdatedAtUtc)
    {
        var aEmployee = new Employee
        {
            FullName = theFullName,
            Position = thePosition,
            HireDate = theHireDate,
            UserId = theUserId
        };

        aEmployee.RestoreState(theId, theStatus, theCreatedAtUtc, theUpdatedAtUtc);
        return aEmployee;
    }
}
