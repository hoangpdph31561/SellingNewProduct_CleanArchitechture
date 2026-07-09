using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Application.Users;

/// <summary>Authenticate a username/password pair and mint an access token.</summary>
public sealed record LoginCommand(string Username, string Password) : IRequest<LoginResult>;

/// <summary>Result of a successful login: the bearer token, when it expires, and who it belongs to.</summary>
public sealed record LoginResult(
    string AccessToken,
    DateTime ExpiresAtUtc,
    Guid UserId,
    string Username,
    UserRole Role);

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUserAuthenticationService myAuthenticationService;
    private readonly IAccessTokenGenerator myTokenGenerator;

    public LoginCommandHandler(
        IUserAuthenticationService theAuthenticationService,
        IAccessTokenGenerator theTokenGenerator)
    {
        myAuthenticationService = theAuthenticationService;
        myTokenGenerator = theTokenGenerator;
    }

    public async Task<LoginResult> Handle(LoginCommand theCommand, CancellationToken theCancellationToken)
    {
        var aUser = await myAuthenticationService.AuthenticateAsync(
            theCommand.Username, theCommand.Password, theCancellationToken);

        // Same message for "no such user" and "wrong password" so the response cannot be used to
        // enumerate valid usernames.
        if (aUser is null)
        {
            throw new UnauthorizedException("Invalid username or password.");
        }

        var aToken = myTokenGenerator.Generate(aUser);
        return new LoginResult(aToken.Value, aToken.ExpiresAtUtc, aUser.Id, aUser.Username, aUser.Role);
    }
}

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
