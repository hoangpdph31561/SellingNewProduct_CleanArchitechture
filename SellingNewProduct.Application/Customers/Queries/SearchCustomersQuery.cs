using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Customers;

public sealed record SearchCustomersQuery(CustomerSearchQuery Criteria) : IRequest<PagedResult<CustomerSummaryView>>;
