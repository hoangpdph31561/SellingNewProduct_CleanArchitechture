using MediatR;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Reports;

public sealed record GetSalesByCategoryQuery : IRequest<IReadOnlyList<CategorySalesView>>;
