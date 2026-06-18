using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;

namespace SellingNewProduct.Application.Employees;

public sealed record GetAllEmployeesQuery : IRequest<IReadOnlyList<Employee>>;

public sealed record GetEmployeeByIdQuery(Guid Id) : IRequest<Employee?>;

public sealed record SearchEmployeesQuery(EmployeeSearchQuery Criteria) : IRequest<PagedResult<EmployeeSummaryView>>;

public sealed class EmployeeQueryHandlers :
    IRequestHandler<GetAllEmployeesQuery, IReadOnlyList<Employee>>,
    IRequestHandler<GetEmployeeByIdQuery, Employee?>,
    IRequestHandler<SearchEmployeesQuery, PagedResult<EmployeeSummaryView>>
{
    private readonly IEmployeeReadRepository myEmployeeRepository;

    public EmployeeQueryHandlers(IEmployeeReadRepository theEmployeeRepository)
    {
        myEmployeeRepository = theEmployeeRepository;
    }

    public Task<IReadOnlyList<Employee>> Handle(GetAllEmployeesQuery theQuery, CancellationToken theCancellationToken)
        => myEmployeeRepository.GetAllAsync(theCancellationToken);

    public Task<Employee?> Handle(GetEmployeeByIdQuery theQuery, CancellationToken theCancellationToken)
        => myEmployeeRepository.GetByIdAsync(theQuery.Id, theCancellationToken);

    public Task<PagedResult<EmployeeSummaryView>> Handle(SearchEmployeesQuery theQuery, CancellationToken theCancellationToken)
        => myEmployeeRepository.SearchAsync(theQuery.Criteria, theCancellationToken);
}
