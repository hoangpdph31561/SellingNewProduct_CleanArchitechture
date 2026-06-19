using MediatR;
using SellingNewProduct.Domain.Categories;

namespace SellingNewProduct.Application.Categories;

/// <summary>Write-side command: create a category. Enforces the "unique name" rule.</summary>
public sealed record CreateCategoryCommand(string Name, string Description) : IRequest<Category>;
