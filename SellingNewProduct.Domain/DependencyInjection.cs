using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Services;

namespace SellingNewProduct.Domain;

/// <summary>
/// Composition for the Domain layer. Binds each inbound (driving) port to its Domain service
/// implementation. Command services hold the business rules that span more than one aggregate;
/// query services forward reads to the read repositories. Both kinds depend only on outbound
/// ports (repositories, unit of work, password hasher), so the core carries no MediatR or
/// infrastructure dependency. Scoped to match those outbound ports.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDomainServices(this IServiceCollection theServices)
    {
        // Command (write) inbound ports.
        theServices.AddScoped<IOrderWriteService, OrderWriteService>();
        theServices.AddScoped<IPaymentWriteService, PaymentWriteService>();
        theServices.AddScoped<IProductWriteService, ProductWriteService>();
        theServices.AddScoped<ICategoryWriteService, CategoryWriteService>();
        theServices.AddScoped<IUserWriteService, UserWriteService>();
        theServices.AddScoped<IUserAuthenticationService, UserAuthenticationService>();
        theServices.AddScoped<ICustomerWriteService, CustomerWriteService>();
        theServices.AddScoped<IEmployeeWriteService, EmployeeWriteService>();

        // Query (read) inbound ports.
        theServices.AddScoped<ICategoryReadService, CategoryReadService>();
        theServices.AddScoped<ICustomerReadService, CustomerReadService>();
        theServices.AddScoped<IEmployeeReadService, EmployeeReadService>();
        theServices.AddScoped<IOrderReadService, OrderReadService>();
        theServices.AddScoped<IPaymentReadService, PaymentReadService>();
        theServices.AddScoped<IProductReadService, ProductReadService>();
        theServices.AddScoped<IReportReadService, ReportReadService>();
        theServices.AddScoped<IUserReadService, UserReadService>();

        return theServices;
    }
}
