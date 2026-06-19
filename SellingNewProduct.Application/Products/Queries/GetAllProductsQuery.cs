using MediatR;
using SellingNewProduct.Domain.Products;

namespace SellingNewProduct.Application.Products;

public sealed record GetAllProductsQuery : IRequest<IReadOnlyList<Product>>;
