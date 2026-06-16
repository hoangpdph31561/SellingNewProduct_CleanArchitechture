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
public sealed class EmployeesController : ControllerBase
{
    private readonly IEmployeeService myEmployeeService;
    private readonly IEmployeeQueries myEmployeeQueries;
    private readonly IValidator<CreateEmployeeRequest> myCreateValidator;

    public EmployeesController(
        IEmployeeService theEmployeeService,
        IEmployeeQueries theEmployeeQueries,
        IValidator<CreateEmployeeRequest> theCreateValidator)
    {
        myEmployeeService = theEmployeeService;
        myEmployeeQueries = theEmployeeQueries;
        myCreateValidator = theCreateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aEmployees = await myEmployeeService.GetAllAsync(theCancellationToken);
        return Ok(aEmployees.Select(e => e.ToResponse()).ToList());
    }

    /// <summary>
    /// Search/filter employees (read side), each row carrying the number of real-sale orders
    /// handled. Filters are optional: "contains" on name or position, plus status; sort by
    /// name/position/hiredate. <c>GET /api/employees/search?theName=an&amp;theSortBy=hiredate</c>
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<EmployeeSummaryView>>> Search(
        [FromQuery] EmployeeSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await myEmployeeQueries.SearchAsync(theQuery, theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<EmployeeResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aEmployee = await myEmployeeService.GetByIdAsync(theId, theCancellationToken);
        return aEmployee is null ? NotFound() : Ok(aEmployee.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeResponse>> Create(CreateEmployeeRequest theRequest, CancellationToken theCancellationToken)
    {
        await myCreateValidator.ValidateAndThrowAsync(theRequest, theCancellationToken);

        var aEmployee = await myEmployeeService.CreateAsync(theRequest.ToCommand(), theCancellationToken);

        return CreatedAtAction(nameof(GetById), new { theId = aEmployee.Id }, aEmployee.ToResponse());
    }
}
