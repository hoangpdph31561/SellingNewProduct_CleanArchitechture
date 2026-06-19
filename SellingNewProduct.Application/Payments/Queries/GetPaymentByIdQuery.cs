using MediatR;
using SellingNewProduct.Domain.Payments;

namespace SellingNewProduct.Application.Payments;

public sealed record GetPaymentByIdQuery(Guid Id) : IRequest<Payment?>;
