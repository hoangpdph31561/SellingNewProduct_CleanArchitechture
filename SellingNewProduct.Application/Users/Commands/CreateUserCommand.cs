using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Application.Users;

/// <summary>Write-side command: create a user. The password is hashed via the Domain contract.</summary>
public sealed record CreateUserCommand(
    string Username,
    string Password,
    string Email,
    int Role) : IRequest<User>;

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

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Role).InclusiveBetween(1, 3);
    }
}
