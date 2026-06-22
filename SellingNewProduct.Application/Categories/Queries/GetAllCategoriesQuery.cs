using MediatR;
using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Interfaces.Inbound;

namespace SellingNewProduct.Application.Categories;

public sealed record GetAllCategoriesQuery : IRequest<IReadOnlyList<Category>>;

public sealed class GetAllCategoriesQueryHandler : IRequestHandler<GetAllCategoriesQuery, IReadOnlyList<Category>>
{
    private readonly ICategoryReadService myCategoryReadService;

    public GetAllCategoriesQueryHandler(ICategoryReadService theCategoryReadService)
    {
        myCategoryReadService = theCategoryReadService;
    }

    public Task<IReadOnlyList<Category>> Handle(GetAllCategoriesQuery theQuery, CancellationToken theCancellationToken)
        => myCategoryReadService.GetAllAsync(theCancellationToken);
}
