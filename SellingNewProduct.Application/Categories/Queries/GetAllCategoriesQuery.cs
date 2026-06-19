using MediatR;
using SellingNewProduct.Domain.Categories;

namespace SellingNewProduct.Application.Categories;

public sealed record GetAllCategoriesQuery : IRequest<IReadOnlyList<Category>>;
