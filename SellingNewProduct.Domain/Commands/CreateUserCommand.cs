namespace SellingNewProduct.Domain.Commands;

/// <summary>Input for <c>IUserService.CreateAsync</c>. <c>Role</c> is the UserRole enum value.</summary>
public sealed record CreateUserCommand(
    string Username,
    string Password,
    string Email,
    int Role);
