using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Interfaces.Inbound;

namespace SellingNewProduct.Application.Categories;

/// <summary>Write-side command: create a category. Enforces the "unique name" rule.</summary>
public sealed record CreateCategoryCommand(string Name, string Description) : IRequest<Category>;

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Category>
{
    private readonly ICategoryWriteService myCategoryWriteService;

    public CreateCategoryCommandHandler(ICategoryWriteService theCategoryWriteService)
    {
        myCategoryWriteService = theCategoryWriteService;
    }

    public Task<Category> Handle(CreateCategoryCommand theCommand, CancellationToken theCancellationToken) =>
        myCategoryWriteService.CreateAsync(theCommand.Name, theCommand.Description, theCancellationToken);
}
