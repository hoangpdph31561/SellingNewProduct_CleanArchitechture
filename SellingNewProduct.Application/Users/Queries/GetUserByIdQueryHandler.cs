using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Application.Users;

public sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, User?>
{
    private readonly IUserReadService myUserReadService;

    public GetUserByIdQueryHandler(IUserReadService theUserReadService)
    {
        myUserReadService = theUserReadService;
    }

    public Task<User?> Handle(GetUserByIdQuery theQuery, CancellationToken theCancellationToken)
        => myUserReadService.GetByIdAsync(theQuery.Id, theCancellationToken);
}
