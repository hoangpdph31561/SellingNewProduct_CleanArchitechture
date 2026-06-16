using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.Users;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Services;

internal sealed class UserService : IUserService
{
    private readonly IUserRepository myUserRepository;
    private readonly IPasswordHasher myPasswordHasher;

    public UserService(IUserRepository theUserRepository, IPasswordHasher thePasswordHasher)
    {
        myUserRepository = theUserRepository;
        myPasswordHasher = thePasswordHasher;
    }

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken theCancellationToken = default)
        => myUserRepository.GetAllAsync(theCancellationToken);

    public Task<User?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myUserRepository.GetByIdAsync(theId, theCancellationToken);

    public async Task<User> CreateAsync(CreateUserCommand theCommand, CancellationToken theCancellationToken = default)
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
