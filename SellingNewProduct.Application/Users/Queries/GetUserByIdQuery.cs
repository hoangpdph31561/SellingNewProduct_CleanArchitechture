using MediatR;
using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Application.Users;

public sealed record GetUserByIdQuery(Guid Id) : IRequest<User?>;
