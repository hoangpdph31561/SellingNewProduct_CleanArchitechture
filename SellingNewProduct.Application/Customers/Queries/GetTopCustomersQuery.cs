using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Customers;

public sealed record GetTopCustomersQuery(int Page, int PageSize) : IRequest<PagedResult<TopCustomerView>>;
