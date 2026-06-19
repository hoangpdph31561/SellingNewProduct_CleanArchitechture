using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Reports;

public sealed class GetLowStockProductsQueryHandler : IRequestHandler<GetLowStockProductsQuery, PagedResult<LowStockProductView>>
{
    private readonly IReportReadService myReportReadService;

    public GetLowStockProductsQueryHandler(IReportReadService theReportReadService)
    {
        myReportReadService = theReportReadService;
    }

    public Task<PagedResult<LowStockProductView>> Handle(GetLowStockProductsQuery theQuery, CancellationToken theCancellationToken)
        => myReportReadService.GetLowStockProductsAsync(theQuery.Threshold, theQuery.Page, theQuery.PageSize, theCancellationToken);
}
