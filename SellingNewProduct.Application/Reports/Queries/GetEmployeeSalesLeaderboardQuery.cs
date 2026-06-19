using MediatR;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Reports;

public sealed record GetEmployeeSalesLeaderboardQuery : IRequest<IReadOnlyList<EmployeeSalesView>>;
