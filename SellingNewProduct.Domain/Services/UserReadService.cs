using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Domain.Services;

/// <summary>Implements the user read port; forwards to the read repository (no business rules).</summary>
public sealed class UserReadService : IUserReadService
{
    private readonly IUserReadRepository myUserRepository;

    public UserReadService(IUserReadRepository theUserRepository)
    {
        myUserRepository = theUserRepository;
    }

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken theCancellationToken = default)
        => myUserRepository.GetAllAsync(theCancellationToken);

    public Task<User?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myUserRepository.GetByIdAsync(theId, theCancellationToken);
}
