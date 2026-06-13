using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Application.Queries;
using SellingNewProduct.Application.ReadModels;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerRepository myCustomerRepository;
    private readonly IOrderQueries myOrderQueries;
    private readonly IValidator<CreateCustomerRequest> myCreateValidator;

    public CustomersController(
        ICustomerRepository theCustomerRepository,
        IOrderQueries theOrderQueries,
        IValidator<CreateCustomerRequest> theCreateValidator)
    {
        myCustomerRepository = theCustomerRepository;
        myOrderQueries = theOrderQueries;
        myCreateValidator = theCreateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerResponse>>> GetAll(CancellationToken theCancellationToken)
    {
        var aCustomers = await myCustomerRepository.GetAllAsync(theCancellationToken);
        return Ok(aCustomers.Select(c => c.ToResponse()).ToList());
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<CustomerResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aCustomer = await myCustomerRepository.GetByIdAsync(theId, theCancellationToken);
        return aCustomer is null ? NotFound() : Ok(aCustomer.ToResponse());
    }

    /// <summary>
    /// A customer's purchase history: total orders, total spent and the order list (with the
    /// selling employee's name). A multi-table read-side query — it does not load each Order aggregate.
    /// </summary>
    [HttpGet("{theId:guid}/orders")]
    public async Task<ActionResult<CustomerOrderHistoryView>> GetOrderHistory(Guid theId, CancellationToken theCancellationToken)
    {
        var aHistory = await myOrderQueries.GetCustomerHistoryAsync(theId, theCancellationToken);
        return aHistory is null ? NotFound() : Ok(aHistory);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> Create(CreateCustomerRequest theRequest, CancellationToken theCancellationToken)
    {
        await myCreateValidator.ValidateAndThrowAsync(theRequest, theCancellationToken);

        var aAddress = Address.Create(
            theRequest.Address.Street,
            theRequest.Address.Ward,
            theRequest.Address.District,
            theRequest.Address.City,
            theRequest.Address.Country);

        var aCustomer = Customer.Create(
            theRequest.FullName,
            Email.Create(theRequest.Email),
            theRequest.PhoneNumber,
            aAddress,
            theRequest.UserId);

        await myCustomerRepository.AddAsync(aCustomer, theCancellationToken);

        return CreatedAtAction(nameof(GetById), new { theId = aCustomer.Id }, aCustomer.ToResponse());
    }
}
