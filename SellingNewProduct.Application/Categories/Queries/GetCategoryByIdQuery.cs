using MediatR;
using SellingNewProduct.Domain.Categories;

namespace SellingNewProduct.Application.Categories;

public sealed record GetCategoryByIdQuery(Guid Id) : IRequest<Category?>;
