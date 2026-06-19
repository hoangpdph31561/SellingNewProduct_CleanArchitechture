using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Employees;

public sealed class SearchEmployeesQueryHandler : IRequestHandler<SearchEmployeesQuery, PagedResult<EmployeeSummaryView>>
{
    private readonly IEmployeeReadService myEmployeeReadService;

    public SearchEmployeesQueryHandler(IEmployeeReadService theEmployeeReadService)
    {
        myEmployeeReadService = theEmployeeReadService;
    }

    public Task<PagedResult<EmployeeSummaryView>> Handle(SearchEmployeesQuery theQuery, CancellationToken theCancellationToken)
        => myEmployeeReadService.SearchAsync(theQuery.Criteria, theCancellationToken);
}
