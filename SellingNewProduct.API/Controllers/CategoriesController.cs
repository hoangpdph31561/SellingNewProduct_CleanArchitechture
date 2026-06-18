using MediatR;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Application.Categories;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ISender mySender;

    public CategoriesController(ISender theSender)
    {
        mySender = theSender;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aCategories = await mySender.Send(new GetAllCategoriesQuery(), theCancellationToken);
        return Ok(aCategories.Select(c => c.ToResponse()).ToList());
    }

    [HttpGet("summaries")]
    public async Task<ActionResult<IReadOnlyList<CategorySummaryView>>> Summaries(CancellationToken theCancellationToken)
    {
        var aResult = await mySender.Send(new GetCategorySummariesQuery(), theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<CategorySummaryView>>> Search(
        [FromQuery] CategorySearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await mySender.Send(new SearchCategoriesQuery(theQuery), theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<CategoryResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aCategory = await mySender.Send(new GetCategoryByIdQuery(theId), theCancellationToken);
        return aCategory is null ? NotFound() : Ok(aCategory.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CreateCategoryRequest theRequest, CancellationToken theCancellationToken)
    {
        var aCategory = await mySender.Send(theRequest.ToCommand(), theCancellationToken);
        return CreatedAtAction(nameof(GetById), new { theId = aCategory.Id }, aCategory.ToResponse());
    }
}
