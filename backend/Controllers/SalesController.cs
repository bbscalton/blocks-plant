using BlocksPlant.Api.Data;
using BlocksPlant.Api.Dtos;
using BlocksPlant.Api.Models;
using BlocksPlant.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize(Roles = "Owner,Cashier")]
public class SalesController : ControllerBase
{
    private readonly AppDbContext _db;

    public SalesController(AppDbContext db) => _db = db;

    [HttpPost]
    public async Task<ActionResult<SaleDto>> Create([FromBody] CreateSaleRequest request)
    {
        if (request.ClientId == Guid.Empty)
            return BadRequest(new { message = "ClientId is required for idempotency." });

        var existing = await _db.Sales
            .Include(s => s.Lines).ThenInclude(l => l.Product)
            .Include(s => s.Customer)
            .Include(s => s.CashierUser)
            .FirstOrDefaultAsync(s => s.ClientId == request.ClientId);
        if (existing is not null)
            return Ok(Map(existing));

        if (request.Lines is null || request.Lines.Count == 0)
            return BadRequest(new { message = "At least one sale line is required." });

        if (request.AmountPaid < 0)
            return BadRequest(new { message = "Amount paid cannot be negative." });

        if (request.FulfillmentType == FulfillmentType.Deliver &&
            string.IsNullOrWhiteSpace(request.DeliveryAddress))
            return BadRequest(new { message = "Delivery address is required for deliveries." });

        await using var tx = await _db.Database.BeginTransactionAsync();

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var lines = new List<SaleLine>();
        decimal subtotal = 0;

        foreach (var lineReq in request.Lines)
        {
            if (lineReq.Quantity <= 0)
                return BadRequest(new { message = "Line quantity must be greater than zero." });

            if (!products.TryGetValue(lineReq.ProductId, out var product))
                return NotFound(new { message = $"Product {lineReq.ProductId} not found." });

            if (product.Quantity < lineReq.Quantity)
                return BadRequest(new
                {
                    message = $"Insufficient stock for {product.Name}. Available: {product.Quantity}, requested: {lineReq.Quantity}."
                });

            var lineTotal = lineReq.Quantity * product.PricePerBlock;
            subtotal += lineTotal;
            lines.Add(new SaleLine
            {
                ProductId = product.Id,
                Quantity = lineReq.Quantity,
                UnitPrice = product.PricePerBlock,
                LineTotal = lineTotal
            });
        }

        if (request.AmountPaid > subtotal)
            return BadRequest(new { message = "Amount paid cannot exceed subtotal." });

        var balanceDue = subtotal - request.AmountPaid;
        if (balanceDue > 0 && request.CustomerId is null)
            return BadRequest(new { message = "Customer is required for credit or partial payment with balance due." });

        Customer? customer = null;
        if (request.CustomerId is int customerId)
        {
            customer = await _db.Customers.FindAsync(customerId);
            if (customer is null)
                return NotFound(new { message = "Customer not found." });
        }

        foreach (var line in lines)
            products[line.ProductId].Quantity -= line.Quantity;

        var sale = new Sale
        {
            CustomerId = request.CustomerId,
            CashierUserId = User.GetUserId(),
            FulfillmentType = request.FulfillmentType,
            DeliveryAddress = request.FulfillmentType == FulfillmentType.Deliver ? request.DeliveryAddress?.Trim() : null,
            DeliveryNotes = request.FulfillmentType == FulfillmentType.Deliver ? request.DeliveryNotes?.Trim() : null,
            DeliveryStatus = request.FulfillmentType == FulfillmentType.Deliver ? DeliveryStatus.Pending : DeliveryStatus.None,
            Subtotal = subtotal,
            AmountPaid = request.AmountPaid,
            BalanceDue = balanceDue,
            CreatedAt = DateTime.UtcNow,
            ClientId = request.ClientId,
            Lines = lines
        };

        if (customer is not null && balanceDue > 0)
            customer.Balance += balanceDue;

        _db.Sales.Add(sale);
        await _db.SaveChangesAsync();

        if (request.AmountPaid > 0)
        {
            _db.Payments.Add(new Payment
            {
                CustomerId = request.CustomerId,
                SaleId = sale.Id,
                Amount = request.AmountPaid,
                Method = PaymentMethod.Cash,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = User.GetUserId()
            });
            await _db.SaveChangesAsync();
        }

        await tx.CommitAsync();

        await _db.Entry(sale).Reference(s => s.CashierUser).LoadAsync();
        await _db.Entry(sale).Reference(s => s.Customer).LoadAsync();
        foreach (var line in sale.Lines)
            await _db.Entry(line).Reference(l => l.Product).LoadAsync();

        return Ok(Map(sale));
    }

    [HttpGet]
    public async Task<ActionResult<List<SaleDto>>> List([FromQuery] int take = 100)
    {
        take = Math.Clamp(take, 1, 500);
        var sales = await _db.Sales
            .Include(s => s.Lines).ThenInclude(l => l.Product)
            .Include(s => s.Customer)
            .Include(s => s.CashierUser)
            .OrderByDescending(s => s.CreatedAt)
            .Take(take)
            .ToListAsync();

        return sales.Select(Map).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SaleDto>> GetById(int id)
    {
        var sale = await _db.Sales
            .Include(s => s.Lines).ThenInclude(l => l.Product)
            .Include(s => s.Customer)
            .Include(s => s.CashierUser)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sale is null) return NotFound();
        return Map(sale);
    }

    private static SaleDto Map(Sale s) => new(
        s.Id,
        s.CustomerId,
        s.Customer?.Name,
        s.CashierUserId,
        s.CashierUser?.FullName ?? string.Empty,
        s.FulfillmentType,
        s.DeliveryAddress,
        s.DeliveryNotes,
        s.DeliveryStatus,
        s.Subtotal,
        s.AmountPaid,
        s.BalanceDue,
        s.CreatedAt,
        s.ClientId,
        s.Lines.Select(l => new SaleLineDto(
            l.Id,
            l.ProductId,
            l.Product?.Name ?? string.Empty,
            l.Quantity,
            l.UnitPrice,
            l.LineTotal)).ToList());
}
