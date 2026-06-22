using MediatR;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Application.Products;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ProductsController : ControllerBase
{
    private readonly ISender mySender;

    public ProductsController(ISender theSender)
    {
        mySender = theSender;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aProducts = await mySender.Send(new GetAllProductsQuery(), theCancellationToken);
        return Ok(aProducts.Select(p => p.ToResponse()).ToList());
    }

    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<ProductSummaryView>>> Search(
        [FromQuery] ProductSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await mySender.Send(new SearchProductsQuery(theQuery), theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aProduct = await mySender.Send(new GetProductByIdQuery(theId), theCancellationToken);
        return aProduct is null ? NotFound() : Ok(aProduct.ToResponse());
    }

    [HttpGet("{theId:guid}/summary")]
    public async Task<ActionResult<ProductSummaryView>> GetSummary(Guid theId, CancellationToken theCancellationToken)
    {
        var aView = await mySender.Send(new GetProductSummaryByIdQuery(theId), theCancellationToken);
        return aView is null ? NotFound() : Ok(aView);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest theRequest, CancellationToken theCancellationToken)
    {
        var aProduct = await mySender.Send(theRequest.ToCommand(), theCancellationToken);
        return CreatedAtAction(nameof(GetById), new { theId = aProduct.Id }, aProduct.ToResponse());
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> CreateMany(BulkCreateProductsRequest theRequest, CancellationToken theCancellationToken)
    {
        var aCommand = new CreateManyProductsCommand(theRequest.Items.Select(i => i.ToCommand()).ToList());
        var aProducts = await mySender.Send(aCommand, theCancellationToken);
        return Ok(aProducts.Select(p => p.ToResponse()).ToList());
    }
}
