using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Payments;

public sealed record SearchPaymentsQuery(PaymentSearchQuery Criteria) : IRequest<PagedResult<PaymentSummaryView>>;
