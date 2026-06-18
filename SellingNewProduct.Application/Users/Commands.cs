using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.Users;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Application.Users;

/// <summary>Write-side command: create a user. The password is hashed via the Domain contract.</summary>
public sealed record CreateUserCommand(
    string Username,
    string Password,
    string Email,
    int Role) : IRequest<User>;

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

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, User>
{
    private readonly IUserWriteRepository myUserRepository;
    private readonly IPasswordHasher myPasswordHasher;

    public CreateUserCommandHandler(IUserWriteRepository theUserRepository, IPasswordHasher thePasswordHasher)
    {
        myUserRepository = theUserRepository;
        myPasswordHasher = thePasswordHasher;
    }

    public async Task<User> Handle(CreateUserCommand theCommand, CancellationToken theCancellationToken)
    {
        var aUser = User.Create(
            theCommand.Username,
            myPasswordHasher.Hash(theCommand.Password),
            Email.Create(theCommand.Email),
            (UserRole)theCommand.Role);

        await myUserRepository.AddAsync(aUser, theCancellationToken);
        return aUser;
    }
}
