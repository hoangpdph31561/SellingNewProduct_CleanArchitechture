using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Application.Users;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, User>
{
    private readonly IUserWriteService myUserWriteService;

    public CreateUserCommandHandler(IUserWriteService theUserWriteService)
    {
        myUserWriteService = theUserWriteService;
    }

    public Task<User> Handle(CreateUserCommand theCommand, CancellationToken theCancellationToken) =>
        myUserWriteService.CreateAsync(
            theCommand.Username,
            theCommand.Password,
            theCommand.Email,
            (UserRole)theCommand.Role,
            theCancellationToken);
}
