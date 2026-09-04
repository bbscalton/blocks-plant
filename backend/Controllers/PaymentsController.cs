using BlocksPlant.Api.Data;
using BlocksPlant.Api.Dtos;
using BlocksPlant.Api.Models;
using BlocksPlant.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize(Roles = "Owner,Cashier")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PaymentsController(AppDbContext db) => _db = db;

    [HttpPost]
    public async Task<ActionResult<PaymentDto>> Create([FromBody] PaymentRequest request)
    {
        if (request.Amount <= 0)
            return BadRequest(new { message = "Amount must be greater than zero." });

        if (request.CustomerId is null && request.SaleId is null)
            return BadRequest(new { message = "CustomerId or SaleId is required." });

        await using var tx = await _db.Database.BeginTransactionAsync();

        Customer? customer = null;
        Sale? sale = null;

        if (request.SaleId is int saleId)
        {
            sale = await _db.Sales.Include(s => s.Customer).FirstOrDefaultAsync(s => s.Id == saleId);
            if (sale is null) return NotFound(new { message = "Sale not found." });
            if (sale.BalanceDue <= 0)
                return BadRequest(new { message = "Sale has no balance due." });
            if (request.Amount > sale.BalanceDue)
                return BadRequest(new { message = $"Payment exceeds sale balance due ({sale.BalanceDue:F2})." });

            sale.AmountPaid += request.Amount;
            sale.BalanceDue -= request.Amount;
            customer = sale.Customer;
            if (customer is null && sale.CustomerId is int cid)
                customer = await _db.Customers.FindAsync(cid);
        }
        else if (request.CustomerId is int customerId)
        {
            customer = await _db.Customers.FindAsync(customerId);
            if (customer is null) return NotFound(new { message = "Customer not found." });
            if (customer.Balance <= 0)
                return BadRequest(new { message = "Customer has no outstanding balance." });
            if (request.Amount > customer.Balance)
                return BadRequest(new { message = $"Payment exceeds customer balance ({customer.Balance:F2})." });
        }

        if (customer is not null)
            customer.Balance = Math.Max(0, customer.Balance - request.Amount);

        var payment = new Payment
        {
            CustomerId = customer?.Id ?? request.CustomerId,
            SaleId = sale?.Id ?? request.SaleId,
            Amount = request.Amount,
            Method = request.Method,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = User.GetUserId()
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        var user = await _db.Users.FindAsync(payment.CreatedByUserId);
        return Ok(new PaymentDto(
            payment.Id,
            payment.CustomerId,
            payment.SaleId,
            payment.Amount,
            payment.Method,
            payment.CreatedAt,
            user?.FullName ?? string.Empty));
    }
}
