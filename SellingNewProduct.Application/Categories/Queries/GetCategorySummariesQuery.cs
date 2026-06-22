using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Categories;

public sealed record GetCategorySummariesQuery : IRequest<IReadOnlyList<CategorySummaryView>>;

public sealed class GetCategorySummariesQueryHandler : IRequestHandler<GetCategorySummariesQuery, IReadOnlyList<CategorySummaryView>>
{
    private readonly ICategoryReadService myCategoryReadService;

    public GetCategorySummariesQueryHandler(ICategoryReadService theCategoryReadService)
    {
        myCategoryReadService = theCategoryReadService;
    }

    public Task<IReadOnlyList<CategorySummaryView>> Handle(GetCategorySummariesQuery theQuery, CancellationToken theCancellationToken)
        => myCategoryReadService.GetSummariesAsync(theCancellationToken);
}
