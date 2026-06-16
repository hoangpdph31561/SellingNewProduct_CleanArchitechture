namespace SellingNewProduct.Domain.Commands;

/// <summary>Input for <c>ICategoryService.CreateAsync</c>.</summary>
public sealed record CreateCategoryCommand(
    string Name,
    string Description);
