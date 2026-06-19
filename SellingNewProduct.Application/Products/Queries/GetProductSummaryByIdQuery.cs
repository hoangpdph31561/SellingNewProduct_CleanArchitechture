using MediatR;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Products;

public sealed record GetProductSummaryByIdQuery(Guid Id) : IRequest<ProductSummaryView?>;
