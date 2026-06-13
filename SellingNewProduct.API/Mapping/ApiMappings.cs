using SellingNewProduct.API.Contracts;
using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Users;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.API.Mapping;

/// <summary>Manual mapping from domain entities to API response DTOs.</summary>
internal static class ApiMappings
{
    public static AddressDto ToDto(this Address theAddress) =>
        new(theAddress.Street, theAddress.Ward, theAddress.District, theAddress.City, theAddress.Country);

    public static CategoryResponse ToResponse(this Category theCategory) =>
        new(theCategory.Id, theCategory.Name, theCategory.Description, theCategory.Status.ToString());

    public static ProductResponse ToResponse(this Product theProduct) =>
        new(theProduct.Id,
            theProduct.Name,
            theProduct.Sku.Value,
            theProduct.Color,
            (int)theProduct.Size,
            theProduct.Price.Amount,
            theProduct.Price.Currency,
            theProduct.StockQuantity,
            theProduct.CategoryId,
            theProduct.Status.ToString());

    public static CustomerResponse ToResponse(this Customer theCustomer) =>
        new(theCustomer.Id,
            theCustomer.FullName,
            theCustomer.Email.Value,
            theCustomer.PhoneNumber,
            theCustomer.DefaultAddress.ToDto(),
            theCustomer.UserId,
            theCustomer.Status.ToString());

    public static UserResponse ToResponse(this User theUser) =>
        new(theUser.Id, theUser.Username, theUser.Email.Value, (int)theUser.Role, theUser.Status.ToString());

    public static EmployeeResponse ToResponse(this Employee theEmployee) =>
        new(theEmployee.Id,
            theEmployee.FullName,
            theEmployee.Position,
            theEmployee.HireDate,
            theEmployee.UserId,
            theEmployee.Status.ToString());

    public static OrderResponse ToResponse(this Order theOrder) =>
        new(theOrder.Id,
            theOrder.CustomerId,
            theOrder.EmployeeId,
            theOrder.OrderStatus.ToString(),
            theOrder.OrderDate,
            theOrder.ShippingAddress.ToDto(),
            theOrder.TotalAmount.Amount,
            theOrder.TotalAmount.Currency,
            theOrder.Details.Select(ToResponse).ToList(),
            theOrder.Status.ToString());

    public static OrderDetailResponse ToResponse(this OrderDetail theDetail) =>
        new(theDetail.Id,
            theDetail.ProductId,
            theDetail.ProductName,
            theDetail.UnitPrice.Amount,
            theDetail.Quantity,
            theDetail.LineTotal.Amount);

    public static PaymentResponse ToResponse(this Payment thePayment) =>
        new(thePayment.Id,
            thePayment.OrderId,
            thePayment.Amount.Amount,
            thePayment.Amount.Currency,
            (int)thePayment.Method,
            thePayment.PaymentStatus.ToString(),
            thePayment.PaidAtUtc,
            thePayment.Status.ToString());
}
