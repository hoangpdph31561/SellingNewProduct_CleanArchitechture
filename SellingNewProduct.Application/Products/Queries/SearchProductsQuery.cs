using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Products;

/// <summary>Wraps the catalogue search criteria (filters/paging/sorting) as a MediatR query.</summary>
public sealed record SearchProductsQuery(ProductSearchQuery Criteria) : IRequest<PagedResult<ProductSummaryView>>;
