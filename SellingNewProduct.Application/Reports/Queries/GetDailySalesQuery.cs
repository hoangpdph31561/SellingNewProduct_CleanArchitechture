using MediatR;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Reports;

public sealed record GetDailySalesQuery(DateTime? FromUtc, DateTime? ToUtc) : IRequest<IReadOnlyList<DailySalesView>>;
