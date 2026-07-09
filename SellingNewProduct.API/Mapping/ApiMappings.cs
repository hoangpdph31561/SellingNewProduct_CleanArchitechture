using SellingNewProduct.API.Contracts;
using SellingNewProduct.Application.Categories;
using SellingNewProduct.Application.Customers;
using SellingNewProduct.Application.Employees;
using SellingNewProduct.Application.Orders;
using SellingNewProduct.Application.Payments;
using SellingNewProduct.Application.Products;
using SellingNewProduct.Application.Users;
using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Users;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.API.Mapping;

/// <summary>
/// Manual mapping at the API boundary: domain entities to response DTOs, and request DTOs
/// to domain command objects. Kept explicit on purpose (no AutoMapper) so mapping mistakes
/// surface at compile time and encapsulation is preserved.
/// </summary>
internal static class ApiMappings
{
    public static AddressDto ToDto(this Address theAddress) =>
        new(theAddress.Street, theAddress.Ward, theAddress.District, theAddress.City, theAddress.Country);

    // ----- Request DTO -> domain command -----

    public static CreateProductCommand ToCommand(this CreateProductRequest theRequest) =>
        new(theRequest.Name,
            theRequest.Sku,
            theRequest.Color,
            theRequest.Size,
            theRequest.Price,
            theRequest.Currency,
            theRequest.StockQuantity,
            theRequest.CategoryId);

    public static CreateCategoryCommand ToCommand(this CreateCategoryRequest theRequest) =>
        new(theRequest.Name, theRequest.Description);

    public static CreateCustomerCommand ToCommand(this CreateCustomerRequest theRequest) =>
        new(theRequest.FullName,
            theRequest.Email,
            theRequest.PhoneNumber,
            theRequest.Address.Street,
            theRequest.Address.Ward,
            theRequest.Address.District,
            theRequest.Address.City,
            theRequest.Address.Country,
            theRequest.UserId);

    public static CreateEmployeeCommand ToCommand(this CreateEmployeeRequest theRequest) =>
        new(theRequest.FullName, theRequest.Position, theRequest.HireDate, theRequest.UserId);

    public static PlaceOrderCommand ToCommand(this PlaceOrderRequest theRequest) =>
        new(theRequest.CustomerId,
            theRequest.EmployeeId,
            theRequest.ShippingAddress.Street,
            theRequest.ShippingAddress.Ward,
            theRequest.ShippingAddress.District,
            theRequest.ShippingAddress.City,
            theRequest.ShippingAddress.Country,
            theRequest.Items.Select(i => new OrderItemCommand(i.ProductId, i.Quantity)).ToList());

    public static CreatePaymentCommand ToCommand(this CreatePaymentRequest theRequest) =>
        new(theRequest.OrderId, theRequest.Amount, theRequest.Currency, theRequest.Method);

    public static CreateUserCommand ToCommand(this CreateUserRequest theRequest) =>
        new(theRequest.Username, theRequest.Password, theRequest.Email, theRequest.Role);

    public static LoginCommand ToCommand(this LoginRequest theRequest) =>
        new(theRequest.Username, theRequest.Password);

    // ----- Domain entity -> response DTO -----

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

    public static LoginResponse ToResponse(this LoginResult theResult) =>
        new(theResult.AccessToken,
            theResult.ExpiresAtUtc,
            theResult.UserId,
            theResult.Username,
            theResult.Role.ToString());

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
