namespace SellingNewProduct.API.Contracts;

public sealed record CreateUserRequest(string Username, string Password, string Email, int Role);

public sealed record UserResponse(Guid Id, string Username, string Email, int Role, string Status);

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, Guid UserId, string Username, string Role);
