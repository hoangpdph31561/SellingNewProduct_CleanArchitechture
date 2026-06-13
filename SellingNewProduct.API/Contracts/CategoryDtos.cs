namespace SellingNewProduct.API.Contracts;

public sealed record CreateCategoryRequest(string Name, string Description);

public sealed record CategoryResponse(Guid Id, string Name, string Description, string Status);
