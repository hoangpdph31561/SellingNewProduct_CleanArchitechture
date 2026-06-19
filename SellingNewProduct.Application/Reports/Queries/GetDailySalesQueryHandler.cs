using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Reports;

public sealed class GetDailySalesQueryHandler : IRequestHandler<GetDailySalesQuery, IReadOnlyList<DailySalesView>>
{
    private readonly IReportReadService myReportReadService;

    public GetDailySalesQueryHandler(IReportReadService theReportReadService)
    {
        myReportReadService = theReportReadService;
    }

    public Task<IReadOnlyList<DailySalesView>> Handle(GetDailySalesQuery theQuery, CancellationToken theCancellationToken)
        => myReportReadService.GetDailySalesAsync(theQuery.FromUtc, theQuery.ToUtc, theCancellationToken);
}
