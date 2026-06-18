using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Application.Products;

// ----- Create one product -------------------------------------------------------------------

/// <summary>Write-side command: create a single product. Returns the created aggregate.</summary>
public sealed record CreateProductCommand(
    string Name,
    string Sku,
    string Color,
    int Size,
    decimal Price,
    string Currency,
    int StockQuantity,
    Guid CategoryId) : IRequest<Product>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Size).InclusiveBetween(1, 5);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Product>
{
    private readonly IProductWriteRepository myProductRepository;
    private readonly ICategoryWriteRepository myCategoryRepository;

    public CreateProductCommandHandler(
        IProductWriteRepository theProductRepository,
        ICategoryWriteRepository theCategoryRepository)
    {
        myProductRepository = theProductRepository;
        myCategoryRepository = theCategoryRepository;
    }

    public async Task<Product> Handle(CreateProductCommand theCommand, CancellationToken theCancellationToken)
    {
        // A product must reference an existing category.
        await EnsureCategoryExistsAsync(theCommand.CategoryId, theCancellationToken);

        // Normalize the SKU through its value object, then enforce the "unique SKU" business rule.
        var aSku = Sku.Create(theCommand.Sku);
        if (await myProductRepository.ExistsBySkuAsync(aSku.Value, theCancellationToken))
        {
            throw new ConflictException($"A product with SKU '{aSku.Value}' already exists.");
        }

        var aProduct = ProductFactory.Build(theCommand, aSku);

        await myProductRepository.AddAsync(aProduct, theCancellationToken);
        return aProduct;
    }

    private async Task EnsureCategoryExistsAsync(Guid theCategoryId, CancellationToken theCancellationToken)
    {
        var aCategory = await myCategoryRepository.GetByIdAsync(theCategoryId, theCancellationToken);
        if (aCategory is null)
        {
            throw new NotFoundException($"Category '{theCategoryId}' not found.");
        }
    }
}

// ----- Create many products (bulk import) ---------------------------------------------------

/// <summary>Write-side command: bulk create. The unique-SKU rule is enforced within the batch and the store.</summary>
public sealed record CreateManyProductsCommand(IReadOnlyList<CreateProductCommand> Items)
    : IRequest<IReadOnlyList<Product>>;

public sealed class CreateManyProductsCommandValidator : AbstractValidator<CreateManyProductsCommand>
{
    public CreateManyProductsCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new CreateProductCommandValidator());
    }
}

public sealed class CreateManyProductsCommandHandler : IRequestHandler<CreateManyProductsCommand, IReadOnlyList<Product>>
{
    private readonly IProductWriteRepository myProductRepository;
    private readonly ICategoryWriteRepository myCategoryRepository;

    public CreateManyProductsCommandHandler(
        IProductWriteRepository theProductRepository,
        ICategoryWriteRepository theCategoryRepository)
    {
        myProductRepository = theProductRepository;
        myCategoryRepository = theCategoryRepository;
    }

    public async Task<IReadOnlyList<Product>> Handle(CreateManyProductsCommand theCommand, CancellationToken theCancellationToken)
    {
        var aItems = theCommand.Items;
        if (aItems is null || aItems.Count == 0)
        {
            throw new DomainException("At least one product is required.");
        }

        // Normalize all SKUs first; reject duplicates WITHIN the batch before touching the database.
        var aSkuByIndex = aItems.Select(c => Sku.Create(c.Sku)).ToList();
        var aDuplicateInBatch = aSkuByIndex.GroupBy(s => s.Value).FirstOrDefault(g => g.Count() > 1);
        if (aDuplicateInBatch is not null)
        {
            throw new ConflictException($"SKU '{aDuplicateInBatch.Key}' is repeated in the request.");
        }

        // Every referenced category must exist (check each distinct id once).
        foreach (var aCategoryId in aItems.Select(c => c.CategoryId).Distinct())
        {
            var aCategory = await myCategoryRepository.GetByIdAsync(aCategoryId, theCancellationToken);
            if (aCategory is null)
            {
                throw new NotFoundException($"Category '{aCategoryId}' not found.");
            }
        }

        // None of the SKUs may already exist in the store.
        foreach (var aSku in aSkuByIndex)
        {
            if (await myProductRepository.ExistsBySkuAsync(aSku.Value, theCancellationToken))
            {
                throw new ConflictException($"A product with SKU '{aSku.Value}' already exists.");
            }
        }

        var aProducts = aItems
            .Select((aCommand, aIndex) => ProductFactory.Build(aCommand, aSkuByIndex[aIndex]))
            .ToList();

        await myProductRepository.AddRangeAsync(aProducts, theCancellationToken);
        return aProducts;
    }
}

/// <summary>Shared "command -> Product aggregate" builder used by both create handlers.</summary>
internal static class ProductFactory
{
    public static Product Build(CreateProductCommand theCommand, Sku theSku) =>
        Product.Create(
            theCommand.Name,
            theSku,
            theCommand.Color,
            (Size)theCommand.Size,
            Money.Create(theCommand.Price, theCommand.Currency),
            theCommand.StockQuantity,
            theCommand.CategoryId);
}
