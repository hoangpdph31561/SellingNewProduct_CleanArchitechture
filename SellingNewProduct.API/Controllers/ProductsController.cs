using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
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
    private readonly IValidator<CreateProductRequest> myCreateValidator;

    public ProductsController(
        IProductRepository theProductRepository,
        ICategoryRepository theCategoryRepository,
        IValidator<CreateProductRequest> theCreateValidator)
    {
        myProductRepository = theProductRepository;
        myCategoryRepository = theCategoryRepository;
        myCreateValidator = theCreateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aProducts = await myProductRepository.GetAllAsync(theCancellationToken);
        return Ok(aProducts.Select(p => p.ToResponse()).ToList());
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
