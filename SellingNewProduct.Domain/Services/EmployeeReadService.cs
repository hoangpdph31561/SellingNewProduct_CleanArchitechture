using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Services;

/// <summary>Implements the employee read port; forwards to the read repository (no business rules).</summary>
public sealed class EmployeeReadService : IEmployeeReadService
{
    private readonly IEmployeeReadRepository myEmployeeRepository;

    public EmployeeReadService(IEmployeeReadRepository theEmployeeRepository)
    {
        myEmployeeRepository = theEmployeeRepository;
    }

    public Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken theCancellationToken = default)
        => myEmployeeRepository.GetAllAsync(theCancellationToken);

    public Task<Employee?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myEmployeeRepository.GetByIdAsync(theId, theCancellationToken);

    public Task<PagedResult<EmployeeSummaryView>> SearchAsync(EmployeeSearchQuery theCriteria, CancellationToken theCancellationToken = default)
        => myEmployeeRepository.SearchAsync(theCriteria, theCancellationToken);
}
