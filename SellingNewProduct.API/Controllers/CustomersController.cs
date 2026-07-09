using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Application.Customers;
using SellingNewProduct.Application.Orders;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CustomersController : ControllerBase
{
    private readonly ISender mySender;

    public CustomersController(ISender theSender)
    {
        mySender = theSender;
    }

    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<CustomerSummaryView>>> Search(
        [FromQuery] CustomerSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await mySender.Send(new SearchCustomersQuery(theQuery), theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet("top")]
    public async Task<ActionResult<PagedResult<TopCustomerView>>> Top(
        [FromQuery] int thePage = 1,
        [FromQuery] int thePageSize = 10,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await mySender.Send(new GetTopCustomersQuery(thePage, thePageSize), theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aCustomers = await mySender.Send(new GetAllCustomersQuery(), theCancellationToken);
        return Ok(aCustomers.Select(c => c.ToResponse()).ToList());
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<CustomerResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aCustomer = await mySender.Send(new GetCustomerByIdQuery(theId), theCancellationToken);
        return aCustomer is null ? NotFound() : Ok(aCustomer.ToResponse());
    }

    [HttpGet("{theId:guid}/orders")]
    public async Task<ActionResult<CustomerOrderHistoryView>> GetOrderHistory(Guid theId, CancellationToken theCancellationToken)
    {
        var aHistory = await mySender.Send(new GetCustomerOrderHistoryQuery(theId), theCancellationToken);
        return aHistory is null ? NotFound() : Ok(aHistory);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> Create(CreateCustomerRequest theRequest, CancellationToken theCancellationToken)
    {
        var aCustomer = await mySender.Send(theRequest.ToCommand(), theCancellationToken);
        return CreatedAtAction(nameof(GetById), new { theId = aCustomer.Id }, aCustomer.ToResponse());
    }

    [HttpDelete]
    public async Task<ActionResult> Delete(DeleteCustomerRequest theRequest, CancellationToken theCancellationToken)
    {
        await mySender.Send(new DeleteCustomerCommand(theRequest.Id), theCancellationToken);
        return NoContent();
    }
}
