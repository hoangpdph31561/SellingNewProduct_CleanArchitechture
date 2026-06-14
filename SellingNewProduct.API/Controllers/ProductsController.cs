using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Application.Common;
using SellingNewProduct.Application.Queries;
using SellingNewProduct.Application.ReadModels;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductRepository myProductRepository;
    private readonly ICategoryRepository myCategoryRepository;
    private readonly IProductQueries myProductQueries;
    private readonly IValidator<CreateProductRequest> myCreateValidator;

    public ProductsController(
        IProductRepository theProductRepository,
        ICategoryRepository theCategoryRepository,
        IProductQueries theProductQueries,
        IValidator<CreateProductRequest> theCreateValidator)
    {
        myProductRepository = theProductRepository;
        myCategoryRepository = theCategoryRepository;
        myProductQueries = theProductQueries;
        myCreateValidator = theCreateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aProducts = await myProductRepository.GetAllAsync(theCancellationToken);
        return Ok(aProducts.Select(p => p.ToResponse()).ToList());
    }

    /// <summary>
    /// Search/filter the catalogue (read side, enriched with the category name). Every filter
    /// is optional. Example:
    /// <c>GET /api/products/search?theName=áo&amp;theCategoryId=...&amp;thePriceFrom=100000&amp;theMaxStock=5&amp;theSortBy=price&amp;theSortDescending=true&amp;thePage=1&amp;thePageSize=20</c>
    /// Returns one page plus the total matching count.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<ProductSummaryView>>> Search(
        [FromQuery] string? theName,
        [FromQuery] Guid? theCategoryId,
        [FromQuery] decimal? thePriceFrom,
        [FromQuery] decimal? thePriceTo,
        [FromQuery] int? theMinStock,
        [FromQuery] int? theMaxStock,
        [FromQuery] EntityStatus? theStatus,
        [FromQuery] int thePage = 1,
        [FromQuery] int thePageSize = PageRequest.DefaultPageSize,
        [FromQuery] string? theSortBy = null,
        [FromQuery] bool theSortDescending = false,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await myProductQueries.SearchAsync(
            theName, theCategoryId, thePriceFrom, thePriceTo, theMinStock, theMaxStock,
            theStatus, thePage, thePageSize, theSortBy, theSortDescending, theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aProduct = await myProductRepository.GetByIdAsync(theId, theCancellationToken);
        return aProduct is null ? NotFound() : Ok(aProduct.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest theRequest, CancellationToken theCancellationToken)
    {
        await myCreateValidator.ValidateAndThrowAsync(theRequest, theCancellationToken);

        var aCategory = await myCategoryRepository.GetByIdAsync(theRequest.CategoryId, theCancellationToken);

        if (aCategory is null)
        {
            return NotFound($"Category '{theRequest.CategoryId}' not found.");
        }

        var aProduct = Product.Create(
            theRequest.Name,
            Sku.Create(theRequest.Sku),
            theRequest.Color,
            (Size)theRequest.Size,
            Money.Create(theRequest.Price, theRequest.Currency),
            theRequest.StockQuantity,
            theRequest.CategoryId);

        await myProductRepository.AddAsync(aProduct, theCancellationToken);

        return CreatedAtAction(nameof(GetById), new { theId = aProduct.Id }, aProduct.ToResponse());
    }
}
