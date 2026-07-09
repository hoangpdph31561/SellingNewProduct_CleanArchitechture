using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Application.Users;

namespace SellingNewProduct.API.Controllers;

/// <summary>
/// Authentication endpoints. Anonymous by design: a caller has no token yet when logging in.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly ISender mySender;

    public AuthController(ISender theSender)
    {
        mySender = theSender;
    }

    /// <summary>Exchange a username/password for a bearer token. Returns 401 on bad credentials.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest theRequest, CancellationToken theCancellationToken)
    {
        var aResult = await mySender.Send(theRequest.ToCommand(), theCancellationToken);
        return Ok(aResult.ToResponse());
    }
}
