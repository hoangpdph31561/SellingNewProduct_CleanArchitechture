using MediatR;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Application.Employees;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class EmployeesController : ControllerBase
{
    private readonly ISender mySender;

    public EmployeesController(ISender theSender)
    {
        mySender = theSender;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aEmployees = await mySender.Send(new GetAllEmployeesQuery(), theCancellationToken);
        return Ok(aEmployees.Select(e => e.ToResponse()).ToList());
    }

    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<EmployeeSummaryView>>> Search(
        [FromQuery] EmployeeSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await mySender.Send(new SearchEmployeesQuery(theQuery), theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<EmployeeResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aEmployee = await mySender.Send(new GetEmployeeByIdQuery(theId), theCancellationToken);
        return aEmployee is null ? NotFound() : Ok(aEmployee.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeResponse>> Create(CreateEmployeeRequest theRequest, CancellationToken theCancellationToken)
    {
        var aEmployee = await mySender.Send(theRequest.ToCommand(), theCancellationToken);
        return CreatedAtAction(nameof(GetById), new { theId = aEmployee.Id }, aEmployee.ToResponse());
    }
}
