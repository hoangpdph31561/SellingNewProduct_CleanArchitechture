using MediatR;
using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Interfaces.Inbound;

namespace SellingNewProduct.Application.Categories;

public sealed record GetCategoryByIdQuery(Guid Id) : IRequest<Category?>;

public sealed class GetCategoryByIdQueryHandler : IRequestHandler<GetCategoryByIdQuery, Category?>
{
    private readonly ICategoryReadService myCategoryReadService;

    public GetCategoryByIdQueryHandler(ICategoryReadService theCategoryReadService)
    {
        myCategoryReadService = theCategoryReadService;
    }

    public Task<Category?> Handle(GetCategoryByIdQuery theQuery, CancellationToken theCancellationToken)
        => myCategoryReadService.GetByIdAsync(theQuery.Id, theCancellationToken);
}
