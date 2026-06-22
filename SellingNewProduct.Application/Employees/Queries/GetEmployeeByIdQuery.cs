using MediatR;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Interfaces.Inbound;

namespace SellingNewProduct.Application.Employees;

public sealed record GetEmployeeByIdQuery(Guid Id) : IRequest<Employee?>;

public sealed class GetEmployeeByIdQueryHandler : IRequestHandler<GetEmployeeByIdQuery, Employee?>
{
    private readonly IEmployeeReadService myEmployeeReadService;

    public GetEmployeeByIdQueryHandler(IEmployeeReadService theEmployeeReadService)
    {
        myEmployeeReadService = theEmployeeReadService;
    }

    public Task<Employee?> Handle(GetEmployeeByIdQuery theQuery, CancellationToken theCancellationToken)
        => myEmployeeReadService.GetByIdAsync(theQuery.Id, theCancellationToken);
}
