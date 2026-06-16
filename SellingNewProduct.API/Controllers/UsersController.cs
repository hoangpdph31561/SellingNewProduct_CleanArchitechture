using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Domain.Abstractions;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService myUserService;
    private readonly IValidator<CreateUserRequest> myCreateValidator;

    public UsersController(IUserService theUserService, IValidator<CreateUserRequest> theCreateValidator)
    {
        myUserService = theUserService;
        myCreateValidator = theCreateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aUsers = await myUserService.GetAllAsync(theCancellationToken);
        return Ok(aUsers.Select(u => u.ToResponse()).ToList());
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aUser = await myUserService.GetByIdAsync(theId, theCancellationToken);
        return aUser is null ? NotFound() : Ok(aUser.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest theRequest, CancellationToken theCancellationToken)
    {
        await myCreateValidator.ValidateAndThrowAsync(theRequest, theCancellationToken);

        var aUser = await myUserService.CreateAsync(theRequest.ToCommand(), theCancellationToken);

        return CreatedAtAction(nameof(GetById), new { theId = aUser.Id }, aUser.ToResponse());
    }
}
