using MediatR;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Interfaces.Inbound;

namespace SellingNewProduct.Application.Employees;

public sealed class GetAllEmployeesQueryHandler : IRequestHandler<GetAllEmployeesQuery, IReadOnlyList<Employee>>
{
    private readonly IEmployeeReadService myEmployeeReadService;

    public GetAllEmployeesQueryHandler(IEmployeeReadService theEmployeeReadService)
    {
        myEmployeeReadService = theEmployeeReadService;
    }

    public Task<IReadOnlyList<Employee>> Handle(GetAllEmployeesQuery theQuery, CancellationToken theCancellationToken)
        => myEmployeeReadService.GetAllAsync(theCancellationToken);
}
