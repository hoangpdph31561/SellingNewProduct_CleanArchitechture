using MediatR;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Application.Users;

public sealed record GetAllUsersQuery : IRequest<IReadOnlyList<User>>;

public sealed record GetUserByIdQuery(Guid Id) : IRequest<User?>;

public sealed class UserQueryHandlers :
    IRequestHandler<GetAllUsersQuery, IReadOnlyList<User>>,
    IRequestHandler<GetUserByIdQuery, User?>
{
    private readonly IUserReadRepository myUserRepository;

    public UserQueryHandlers(IUserReadRepository theUserRepository)
    {
        myUserRepository = theUserRepository;
    }

    public Task<IReadOnlyList<User>> Handle(GetAllUsersQuery theQuery, CancellationToken theCancellationToken)
        => myUserRepository.GetAllAsync(theCancellationToken);

    public Task<User?> Handle(GetUserByIdQuery theQuery, CancellationToken theCancellationToken)
        => myUserRepository.GetByIdAsync(theQuery.Id, theCancellationToken);
}
