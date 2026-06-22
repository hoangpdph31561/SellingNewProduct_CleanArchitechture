using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Domain.Interfaces.Outbound;

public interface IUserWriteRepository : IWriteRepository<User>
{
    /// <summary>Loads a user aggregate (used by the employee-create rule "user must exist").</summary>
    Task<User?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

}
