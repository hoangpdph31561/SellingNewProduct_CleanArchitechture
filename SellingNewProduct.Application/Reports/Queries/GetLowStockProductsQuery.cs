using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Reports;

public sealed record GetLowStockProductsQuery(int Threshold, int Page, int PageSize) : IRequest<PagedResult<LowStockProductView>>;
