using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Payments;

public sealed record GetOutstandingOrdersQuery(int Page, int PageSize) : IRequest<PagedResult<OutstandingOrderView>>;
