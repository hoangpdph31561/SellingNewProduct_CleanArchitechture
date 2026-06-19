using MediatR;
using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Interfaces.Inbound;

namespace SellingNewProduct.Application.Categories;

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
