using MediatR;
using SellingNewProduct.Domain.Products;

namespace SellingNewProduct.Application.Products;

public sealed record GetProductByIdQuery(Guid Id) : IRequest<Product?>;
