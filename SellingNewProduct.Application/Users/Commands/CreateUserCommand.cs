using MediatR;
using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Application.Users;

/// <summary>Write-side command: create a user. The password is hashed via the Domain contract.</summary>
public sealed record CreateUserCommand(
    string Username,
    string Password,
    string Email,
    int Role) : IRequest<User>;
