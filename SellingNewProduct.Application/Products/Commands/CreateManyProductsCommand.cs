using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Products;

namespace SellingNewProduct.Application.Products;

/// <summary>Write-side command: bulk create. The unique-SKU rule is enforced within the batch and the store.</summary>
public sealed record CreateManyProductsCommand(IReadOnlyList<CreateProductCommand> Items)
    : IRequest<IReadOnlyList<Product>>;

public sealed class CreateManyProductsCommandHandler : IRequestHandler<CreateManyProductsCommand, IReadOnlyList<Product>>
{
    private readonly IProductWriteService myProductWriteService;

    public CreateManyProductsCommandHandler(IProductWriteService theProductWriteService)
    {
        myProductWriteService = theProductWriteService;
    }

    public Task<IReadOnlyList<Product>> Handle(CreateManyProductsCommand theCommand, CancellationToken theCancellationToken)
    {
        var aRequests = theCommand.Items.Select(i => i.ToNewProduct()).ToList();
        return myProductWriteService.CreateManyAsync(aRequests, theCancellationToken);
    }
}

public sealed class CreateManyProductsCommandValidator : AbstractValidator<CreateManyProductsCommand>
{
    public CreateManyProductsCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new CreateProductCommandValidator());
    }
}
