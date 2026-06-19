using MediatR;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Categories;

public sealed record GetCategorySummariesQuery : IRequest<IReadOnlyList<CategorySummaryView>>;
