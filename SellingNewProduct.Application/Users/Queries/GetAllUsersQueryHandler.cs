using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Application.Users;

public sealed class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, IReadOnlyList<User>>
{
    private readonly IUserReadService myUserReadService;

    public GetAllUsersQueryHandler(IUserReadService theUserReadService)
    {
        myUserReadService = theUserReadService;
    }

    public Task<IReadOnlyList<User>> Handle(GetAllUsersQuery theQuery, CancellationToken theCancellationToken)
        => myUserReadService.GetAllAsync(theCancellationToken);
}
