namespace SellingNewProduct.API.Contracts;

public sealed record CreateUserRequest(string Username, string Password, string Email, int Role);

public sealed record UserResponse(Guid Id, string Username, string Email, int Role, string Status);
