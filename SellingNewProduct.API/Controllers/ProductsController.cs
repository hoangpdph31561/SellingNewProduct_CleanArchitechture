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
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService myProductService;
    private readonly IProductQueries myProductQueries;
    private readonly IValidator<CreateProductRequest> myCreateValidator;

    public ProductsController(
        IProductService theProductService,
        IProductQueries theProductQueries,
        IValidator<CreateProductRequest> theCreateValidator)
    {
        myProductService = theProductService;
        myProductQueries = theProductQueries;
        myCreateValidator = theCreateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aProducts = await myProductService.GetAllAsync(theCancellationToken);
        return Ok(aProducts.Select(p => p.ToResponse()).ToList());
    }

    /// <summary>
    /// Search/filter the catalogue (read side, enriched with the category name). Every filter
    /// is optional. Example:
    /// <c>GET /api/products/search?theName=ao&amp;theCategoryId=...&amp;thePriceFrom=100000&amp;theMaxStock=5&amp;theSortBy=price&amp;theSortDescending=true&amp;thePage=1&amp;thePageSize=20</c>
    /// Returns one page plus the total matching count.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<ProductSummaryView>>> Search(
        [FromQuery] ProductSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await myProductQueries.SearchAsync(theQuery, theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aProduct = await myProductService.GetByIdAsync(theId, theCancellationToken);
        return aProduct is null ? NotFound() : Ok(aProduct.ToResponse());
    }

    /// <summary>
    /// One product enriched with the category name (read side), the flat counterpart of
    /// <see cref="GetById"/>. <c>GET /api/products/{id}/summary</c>
    /// </summary>
    [HttpGet("{theId:guid}/summary")]
    public async Task<ActionResult<ProductSummaryView>> GetSummary(Guid theId, CancellationToken theCancellationToken)
    {
        var aView = await myProductQueries.GetByIdAsync(theId, theCancellationToken);
        return aView is null ? NotFound() : Ok(aView);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest theRequest, CancellationToken theCancellationToken)
    {
        await myCreateValidator.ValidateAndThrowAsync(theRequest, theCancellationToken);

        var aProduct = await myProductService.CreateAsync(theRequest.ToCommand(), theCancellationToken);

        return CreatedAtAction(nameof(GetById), new { theId = aProduct.Id }, aProduct.ToResponse());
    }
}
