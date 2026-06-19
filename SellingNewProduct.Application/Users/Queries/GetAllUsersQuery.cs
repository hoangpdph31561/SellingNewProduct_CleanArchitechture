using MediatR;
using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Application.Users;

public sealed record GetAllUsersQuery : IRequest<IReadOnlyList<User>>;
