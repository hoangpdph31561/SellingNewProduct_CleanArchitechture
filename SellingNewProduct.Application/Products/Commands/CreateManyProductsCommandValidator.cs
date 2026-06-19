using FluentValidation;

namespace SellingNewProduct.Application.Products;

public sealed class CreateManyProductsCommandValidator : AbstractValidator<CreateManyProductsCommand>
{
    public CreateManyProductsCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new CreateProductCommandValidator());
    }
}
