using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;

namespace SellingNewProduct.Application.Products;

// Read side: every query forwards to IProductReadRepository. No business rules, no mutation.

public sealed record GetAllProductsQuery : IRequest<IReadOnlyList<Product>>;

public sealed record GetProductByIdQuery(Guid Id) : IRequest<Product?>;

public sealed record GetProductSummaryByIdQuery(Guid Id) : IRequest<ProductSummaryView?>;

/// <summary>Wraps the catalogue search criteria (filters/paging/sorting) as a MediatR query.</summary>
public sealed record SearchProductsQuery(ProductSearchQuery Criteria) : IRequest<PagedResult<ProductSummaryView>>;

public sealed class ProductQueryHandlers :
    IRequestHandler<GetAllProductsQuery, IReadOnlyList<Product>>,
    IRequestHandler<GetProductByIdQuery, Product?>,
    IRequestHandler<GetProductSummaryByIdQuery, ProductSummaryView?>,
    IRequestHandler<SearchProductsQuery, PagedResult<ProductSummaryView>>
{
    private readonly IProductReadRepository myProductRepository;

    public ProductQueryHandlers(IProductReadRepository theProductRepository)
    {
        myProductRepository = theProductRepository;
    }

    public Task<IReadOnlyList<Product>> Handle(GetAllProductsQuery theQuery, CancellationToken theCancellationToken)
        => myProductRepository.GetAllAsync(theCancellationToken);

    public Task<Product?> Handle(GetProductByIdQuery theQuery, CancellationToken theCancellationToken)
        => myProductRepository.GetByIdAsync(theQuery.Id, theCancellationToken);

    public Task<ProductSummaryView?> Handle(GetProductSummaryByIdQuery theQuery, CancellationToken theCancellationToken)
        => myProductRepository.GetSummaryByIdAsync(theQuery.Id, theCancellationToken);

    public Task<PagedResult<ProductSummaryView>> Handle(SearchProductsQuery theQuery, CancellationToken theCancellationToken)
        => myProductRepository.SearchAsync(theQuery.Criteria, theCancellationToken);
}
