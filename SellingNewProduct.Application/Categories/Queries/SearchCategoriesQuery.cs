using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Categories;

public sealed record SearchCategoriesQuery(CategorySearchQuery Criteria) : IRequest<PagedResult<CategorySummaryView>>;
