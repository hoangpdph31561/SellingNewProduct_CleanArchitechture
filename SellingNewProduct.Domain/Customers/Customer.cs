using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Customers;

/// <summary>
/// A buying customer. Aggregate root. May optionally be linked to a login
/// account through <see cref="UserId"/> (referenced by id, not by object).
/// </summary>
public sealed class Customer : AggregateRoot<Guid>
{
    public string FullName { get; private set; } = default!;

    public Email Email { get; private set; } = default!;

    public string PhoneNumber { get; private set; } = default!;

    public Address DefaultAddress { get; private set; } = default!;

    public Guid? UserId { get; private set; }

    private Customer()
    {
    }

    private Customer(
        Guid theId,
        string theFullName,
        Email theEmail,
        string thePhoneNumber,
        Address theDefaultAddress,
        Guid? theUserId)
        : base(theId)
    {
        FullName = theFullName;
        Email = theEmail;
        PhoneNumber = thePhoneNumber;
        DefaultAddress = theDefaultAddress;
        UserId = theUserId;
    }

    public static Customer Create(
        string theFullName,
        Email theEmail,
        string thePhoneNumber,
        Address theDefaultAddress,
        Guid? theUserId = null)
    {
        if (string.IsNullOrWhiteSpace(theFullName))
        {
            throw new DomainException("Customer full name is required.");
        }

        if (string.IsNullOrWhiteSpace(thePhoneNumber))
        {
            throw new DomainException("Customer phone number is required.");
        }

        return new Customer(Guid.NewGuid(), theFullName.Trim(), theEmail, thePhoneNumber.Trim(), theDefaultAddress, theUserId);
    }

    public void Rename(string theNewFullName)
    {
        if (string.IsNullOrWhiteSpace(theNewFullName))
        {
            throw new DomainException("Customer full name is required.");
        }

        FullName = theNewFullName.Trim();
        MarkUpdated();
    }

    public void ChangeEmail(Email theNewEmail)
    {
        Email = theNewEmail;
        MarkUpdated();
    }

    public void UpdateAddress(Address theNewAddress)
    {
        DefaultAddress = theNewAddress;
        MarkUpdated();
    }

    public static Customer Rehydrate(
        Guid theId,
        string theFullName,
        Email theEmail,
        string thePhoneNumber,
        Address theDefaultAddress,
        Guid? theUserId,
        EntityStatus theStatus,
        DateTime theCreatedAtUtc,
        DateTime? theUpdatedAtUtc)
    {
        var aCustomer = new Customer
        {
            FullName = theFullName,
            Email = theEmail,
            PhoneNumber = thePhoneNumber,
            DefaultAddress = theDefaultAddress,
            UserId = theUserId
        };

        aCustomer.RestoreState(theId, theStatus, theCreatedAtUtc, theUpdatedAtUtc);
        return aCustomer;
    }
}
