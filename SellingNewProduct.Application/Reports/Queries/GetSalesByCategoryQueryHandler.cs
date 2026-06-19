using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Reports;

public sealed class GetSalesByCategoryQueryHandler : IRequestHandler<GetSalesByCategoryQuery, IReadOnlyList<CategorySalesView>>
{
    private readonly IReportReadService myReportReadService;

    public GetSalesByCategoryQueryHandler(IReportReadService theReportReadService)
    {
        myReportReadService = theReportReadService;
    }

    public Task<IReadOnlyList<CategorySalesView>> Handle(GetSalesByCategoryQuery theQuery, CancellationToken theCancellationToken)
        => myReportReadService.GetSalesByCategoryAsync(theCancellationToken);
}
