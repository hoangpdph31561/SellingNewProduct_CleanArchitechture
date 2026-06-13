using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Repositories;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICategoryRepository myCategoryRepository;
    private readonly IValidator<CreateCategoryRequest> myCreateValidator;

    public CategoriesController(
        ICategoryRepository theCategoryRepository,
        IValidator<CreateCategoryRequest> theCreateValidator)
    {
        myCategoryRepository = theCategoryRepository;
        myCreateValidator = theCreateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aCategories = await myCategoryRepository.GetAllAsync(theCancellationToken);
        return Ok(aCategories.Select(c => c.ToResponse()).ToList());
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<CategoryResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aCategory = await myCategoryRepository.GetByIdAsync(theId, theCancellationToken);
        return aCategory is null ? NotFound() : Ok(aCategory.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CreateCategoryRequest theRequest, CancellationToken theCancellationToken)
    {
        await myCreateValidator.ValidateAndThrowAsync(theRequest, theCancellationToken);

        var aCategory = Category.Create(theRequest.Name, theRequest.Description);
        await myCategoryRepository.AddAsync(aCategory, theCancellationToken);

        return CreatedAtAction(nameof(GetById), new { theId = aCategory.Id }, aCategory.ToResponse());
    }
}
