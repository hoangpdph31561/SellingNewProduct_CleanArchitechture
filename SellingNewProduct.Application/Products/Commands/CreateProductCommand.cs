using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Products;

namespace SellingNewProduct.Application.Products;

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

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Product>
{
    private readonly IProductWriteService myProductWriteService;

    public CreateProductCommandHandler(IProductWriteService theProductWriteService)
    {
        myProductWriteService = theProductWriteService;
    }

    public Task<Product> Handle(CreateProductCommand theCommand, CancellationToken theCancellationToken) =>
        myProductWriteService.CreateAsync(theCommand.ToNewProduct(), theCancellationToken);
}

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
