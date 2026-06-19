using MediatR;
using SellingNewProduct.Domain.Payments;

namespace SellingNewProduct.Application.Payments;

public sealed record CompletePaymentCommand(Guid Id) : IRequest<Payment>;
