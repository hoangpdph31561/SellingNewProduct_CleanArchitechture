using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Application.Users;

namespace SellingNewProduct.API.Controllers;

// User administration is Admin-only, except account creation which is left open so the very first
// account can be bootstrapped (there is no admin yet to authorize it). Lock Create down once a real
// registration flow / seeded admin exists.
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public sealed class UsersController : ControllerBase
{
    private readonly ISender mySender;

    public UsersController(ISender theSender)
    {
        mySender = theSender;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aUsers = await mySender.Send(new GetAllUsersQuery(), theCancellationToken);
        return Ok(aUsers.Select(u => u.ToResponse()).ToList());
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aUser = await mySender.Send(new GetUserByIdQuery(theId), theCancellationToken);
        return aUser is null ? NotFound() : Ok(aUser.ToResponse());
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest theRequest, CancellationToken theCancellationToken)
    {
        var aUser = await mySender.Send(theRequest.ToCommand(), theCancellationToken);
        return CreatedAtAction(nameof(GetById), new { theId = aUser.Id }, aUser.ToResponse());
    }
}
