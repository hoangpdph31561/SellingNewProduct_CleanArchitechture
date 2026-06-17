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
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService myCustomerService;
    private readonly IOrderService myOrderService;

    public CustomersController(
        ICustomerService theCustomerService,
        IOrderService theOrderService)
    {
        myCustomerService = theCustomerService;
        myOrderService = theOrderService;
    }

    /// <summary>
    /// Search/filter customers (read side). Every filter is optional: a "contains" on name,
    /// email, phone or city, plus status; sort by name/email/city.
    /// Example: <c>GET /api/customers/search?theCity=Ha&amp;theSortBy=name&amp;thePage=1&amp;thePageSize=20</c>
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<CustomerSummaryView>>> Search(
        [FromQuery] CustomerSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await myCustomerService.SearchAsync(theQuery, theCancellationToken);
        return Ok(aResult);
    }

    /// <summary>
    /// Customers ranked by total amount spent (real sales), paginated. Page 1 is the "top N".
    /// <c>GET /api/customers/top?thePage=1&amp;thePageSize=10</c>
    /// </summary>
    [HttpGet("top")]
    public async Task<ActionResult<PagedResult<TopCustomerView>>> Top(
        [FromQuery] int thePage = 1,
        [FromQuery] int thePageSize = 10,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await myCustomerService.GetTopCustomersAsync(thePage, thePageSize, theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aCustomers = await myCustomerService.GetAllAsync(theCancellationToken);
        return Ok(aCustomers.Select(c => c.ToResponse()).ToList());
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<CustomerResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aCustomer = await myCustomerService.GetByIdAsync(theId, theCancellationToken);
        return aCustomer is null ? NotFound() : Ok(aCustomer.ToResponse());
    }

    /// <summary>
    /// A customer's purchase history: total orders, total spent and the order list (with the
    /// selling employee's name). A multi-table read-side query — it does not load each Order aggregate.
    /// </summary>
    [HttpGet("{theId:guid}/orders")]
    public async Task<ActionResult<CustomerOrderHistoryView>> GetOrderHistory(Guid theId, CancellationToken theCancellationToken)
    {
        var aHistory = await myOrderService.GetCustomerHistoryAsync(theId, theCancellationToken);
        return aHistory is null ? NotFound() : Ok(aHistory);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> Create(CreateCustomerRequest theRequest, CancellationToken theCancellationToken)
    {
        var aCustomer = await myCustomerService.CreateAsync(theRequest.ToCommand(), theCancellationToken);

        return CreatedAtAction(nameof(GetById), new { theId = aCustomer.Id }, aCustomer.ToResponse());
    }

    [HttpDelete]
    public async Task<ActionResult> Delete(DeleteCustomerRequest theRequest, CancellationToken theCancellationToken)
    {
        await myCustomerService.DeleteAsync(theRequest.Id, theCancellationToken);
        return NoContent();
    }
}
