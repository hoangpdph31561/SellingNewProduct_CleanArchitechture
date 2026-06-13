using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Repositories;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class EmployeesController : ControllerBase
{
    private readonly IEmployeeRepository myEmployeeRepository;
    private readonly IUserRepository myUserRepository;
    private readonly IValidator<CreateEmployeeRequest> myCreateValidator;

    public EmployeesController(
        IEmployeeRepository theEmployeeRepository,
        IUserRepository theUserRepository,
        IValidator<CreateEmployeeRequest> theCreateValidator)
    {
        myEmployeeRepository = theEmployeeRepository;
        myUserRepository = theUserRepository;
        myCreateValidator = theCreateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aEmployees = await myEmployeeRepository.GetAllAsync(theCancellationToken);
        return Ok(aEmployees.Select(e => e.ToResponse()).ToList());
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<EmployeeResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aEmployee = await myEmployeeRepository.GetByIdAsync(theId, theCancellationToken);
        return aEmployee is null ? NotFound() : Ok(aEmployee.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeResponse>> Create(CreateEmployeeRequest theRequest, CancellationToken theCancellationToken)
    {
        await myCreateValidator.ValidateAndThrowAsync(theRequest, theCancellationToken);

        var aUser = await myUserRepository.GetByIdAsync(theRequest.UserId, theCancellationToken);

        if (aUser is null)
        {
            return NotFound($"User '{theRequest.UserId}' not found.");
        }

        var aEmployee = Employee.Create(theRequest.FullName, theRequest.Position, theRequest.HireDate, theRequest.UserId);
        await myEmployeeRepository.AddAsync(aEmployee, theCancellationToken);

        return CreatedAtAction(nameof(GetById), new { theId = aEmployee.Id }, aEmployee.ToResponse());
    }
}
