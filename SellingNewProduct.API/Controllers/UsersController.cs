using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.API.Security;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.Users;
using SellingNewProduct.Domain.ValueObjects;
using DomainUser = SellingNewProduct.Domain.Users.User;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserRepository myUserRepository;
    private readonly IValidator<CreateUserRequest> myCreateValidator;

    public UsersController(IUserRepository theUserRepository, IValidator<CreateUserRequest> theCreateValidator)
    {
        myUserRepository = theUserRepository;
        myCreateValidator = theCreateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aUsers = await myUserRepository.GetAllAsync(theCancellationToken);
        return Ok(aUsers.Select(u => u.ToResponse()).ToList());
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aUser = await myUserRepository.GetByIdAsync(theId, theCancellationToken);
        return aUser is null ? NotFound() : Ok(aUser.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest theRequest, CancellationToken theCancellationToken)
    {
        await myCreateValidator.ValidateAndThrowAsync(theRequest, theCancellationToken);

        var aUser = DomainUser.Create(
            theRequest.Username,
            PasswordHasher.Hash(theRequest.Password),
            Email.Create(theRequest.Email),
            (UserRole)theRequest.Role);

        await myUserRepository.AddAsync(aUser, theCancellationToken);

        return CreatedAtAction(nameof(GetById), new { theId = aUser.Id }, aUser.ToResponse());
    }
}
