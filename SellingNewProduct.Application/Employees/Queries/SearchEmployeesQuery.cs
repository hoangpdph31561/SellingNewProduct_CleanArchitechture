using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Employees;

public sealed record SearchEmployeesQuery(EmployeeSearchQuery Criteria) : IRequest<PagedResult<EmployeeSummaryView>>;
