using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Reports;

public sealed record GetBestSellingProductsQuery(int Page, int PageSize) : IRequest<PagedResult<BestSellingProductView>>;
