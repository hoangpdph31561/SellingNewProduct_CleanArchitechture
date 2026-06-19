using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Reports;

public sealed class GetEmployeeSalesLeaderboardQueryHandler : IRequestHandler<GetEmployeeSalesLeaderboardQuery, IReadOnlyList<EmployeeSalesView>>
{
    private readonly IReportReadService myReportReadService;

    public GetEmployeeSalesLeaderboardQueryHandler(IReportReadService theReportReadService)
    {
        myReportReadService = theReportReadService;
    }

    public Task<IReadOnlyList<EmployeeSalesView>> Handle(GetEmployeeSalesLeaderboardQuery theQuery, CancellationToken theCancellationToken)
        => myReportReadService.GetEmployeeSalesLeaderboardAsync(theCancellationToken);
}
