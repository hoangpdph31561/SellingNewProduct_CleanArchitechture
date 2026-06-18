using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Repositories;

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
    private readonly ICategoryWriteRepository myCategoryRepository;

    public CreateCategoryCommandHandler(ICategoryWriteRepository theCategoryRepository)
    {
        myCategoryRepository = theCategoryRepository;
    }

    public async Task<Category> Handle(CreateCategoryCommand theCommand, CancellationToken theCancellationToken)
    {
        // Create (the aggregate validates + trims) then use the normalized name for the uniqueness check.
        var aCategory = Category.Create(theCommand.Name, theCommand.Description);

        if (await myCategoryRepository.ExistsByNameAsync(aCategory.Name, theCancellationToken))
        {
            throw new ConflictException($"Category '{aCategory.Name}' already exists.");
        }

        await myCategoryRepository.AddAsync(aCategory, theCancellationToken);
        return aCategory;
    }
}
