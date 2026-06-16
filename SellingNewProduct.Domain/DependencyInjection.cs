using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Services;

namespace SellingNewProduct.Domain;

/// <summary>
/// Registers the domain services. This lets implementations (e.g. <c>CategoryService</c>)
/// stay <c>internal</c> while the API still consumes them through their interfaces.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDomainServices(this IServiceCollection theServices)
    {
        theServices.AddScoped<ICategoryService, CategoryService>();
        theServices.AddScoped<IProductService, ProductService>();
        theServices.AddScoped<ICustomerService, CustomerService>();
        theServices.AddScoped<IEmployeeService, EmployeeService>();
        theServices.AddScoped<IOrderService, OrderService>();
        theServices.AddScoped<IPaymentService, PaymentService>();
        theServices.AddScoped<IUserService, UserService>();
        return theServices;
    }
}
