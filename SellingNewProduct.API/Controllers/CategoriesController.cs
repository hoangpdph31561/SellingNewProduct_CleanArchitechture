using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICategoryService myCategoryService;
    private readonly ICategoryQueries myCategoryQueries;
    private readonly IValidator<CreateCategoryRequest> myCreateValidator;

    public CategoriesController(
        ICategoryService theCategoryService,
        ICategoryQueries theCategoryQueries,
        IValidator<CreateCategoryRequest> theCreateValidator)
    {
        myCategoryService = theCategoryService;
        myCategoryQueries = theCategoryQueries;
        myCreateValidator = theCreateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aCategories = await myCategoryService.GetAllAsync(theCancellationToken);
        return Ok(aCategories.Select(c => c.ToResponse()).ToList());
    }

    /// <summary>
    /// Every category enriched with its product count and total stock value (read side).
    /// <c>GET /api/categories/summaries</c>
    /// </summary>
    [HttpGet("summaries")]
    public async Task<ActionResult<IReadOnlyList<CategorySummaryView>>> Summaries(CancellationToken theCancellationToken)
    {
        var aResult = await myCategoryQueries.GetCategorySummariesAsync(theCancellationToken);
        return Ok(aResult);
    }

    /// <summary>
    /// Search categories by a "contains" on the name (optional), each enriched with product
    /// count and stock value, paginated. <c>GET /api/categories/search?theName=ao&amp;thePage=1</c>
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<CategorySummaryView>>> Search(
        [FromQuery] CategorySearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await myCategoryQueries.SearchAsync(theQuery, theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<CategoryResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aCategory = await myCategoryService.GetByIdAsync(theId, theCancellationToken);
        return aCategory is null ? NotFound() : Ok(aCategory.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CreateCategoryRequest theRequest, CancellationToken theCancellationToken)
    {
        // The API only validates the request SHAPE (Name != null, length...). The
        // business rule (unique name) lives in CategoryService.
        await myCreateValidator.ValidateAndThrowAsync(theRequest, theCancellationToken);

        var aCategory = await myCategoryService.CreateAsync(theRequest.ToCommand(), theCancellationToken);

        return CreatedAtAction(nameof(GetById), new { theId = aCategory.Id }, aCategory.ToResponse());
    }
}
