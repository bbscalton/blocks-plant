using BlocksPlant.Api.Data;
using BlocksPlant.Api.Dtos;
using BlocksPlant.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize(Roles = "Owner,Cashier")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<CustomerDto>>> GetAll([FromQuery] string? search = null)
    {
        var query = _db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(s) || (c.Phone != null && c.Phone.Contains(s)));
        }

        var items = await query.OrderBy(c => c.Name).ToListAsync();
        return items.Select(Map).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDto>> GetById(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return NotFound();
        return Map(customer);
    }

    [HttpGet("debtors")]
    public async Task<ActionResult<List<CustomerDto>>> GetDebtors()
    {
        var items = await _db.Customers
            .Where(c => c.Balance > 0)
            .OrderByDescending(c => c.Balance)
            .ToListAsync();
        return items.Select(Map).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Customer name is required." });

        var customer = new Customer
        {
            Name = request.Name.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            Balance = 0
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, Map(customer));
    }

    private static CustomerDto Map(Customer c) =>
        new(c.Id, c.Name, c.Phone, c.Address, c.Balance);
}
