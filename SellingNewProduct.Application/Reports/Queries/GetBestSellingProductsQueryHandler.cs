using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Reports;

public sealed class GetBestSellingProductsQueryHandler : IRequestHandler<GetBestSellingProductsQuery, PagedResult<BestSellingProductView>>
{
    private readonly IReportReadService myReportReadService;

    public GetBestSellingProductsQueryHandler(IReportReadService theReportReadService)
    {
        myReportReadService = theReportReadService;
    }

    public Task<PagedResult<BestSellingProductView>> Handle(GetBestSellingProductsQuery theQuery, CancellationToken theCancellationToken)
        => myReportReadService.GetBestSellingProductsAsync(theQuery.Page, theQuery.PageSize, theCancellationToken);
}
