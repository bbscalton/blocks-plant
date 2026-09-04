using BlocksPlant.Api.Data;
using BlocksPlant.Api.Dtos;
using BlocksPlant.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Controllers;

[ApiController]
[Route("api/deliveries")]
[Authorize(Roles = "Owner,Cashier")]
public class DeliveriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public DeliveriesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<DeliveryDto>>> GetPending([FromQuery] bool includeDelivered = false)
    {
        var query = _db.Sales
            .Include(s => s.Customer)
            .Where(s => s.FulfillmentType == FulfillmentType.Deliver);

        if (!includeDelivered)
            query = query.Where(s => s.DeliveryStatus == DeliveryStatus.Pending);

        var items = await query.OrderBy(s => s.CreatedAt).ToListAsync();
        return items.Select(s => new DeliveryDto(
            s.Id,
            s.Customer?.Name,
            s.DeliveryAddress,
            s.DeliveryNotes,
            s.DeliveryStatus,
            s.Subtotal,
            s.CreatedAt)).ToList();
    }

    [HttpPatch("{saleId:int}")]
    public async Task<ActionResult<DeliveryDto>> UpdateStatus(int saleId, [FromBody] UpdateDeliveryStatusRequest request)
    {
        var sale = await _db.Sales.Include(s => s.Customer).FirstOrDefaultAsync(s => s.Id == saleId);
        if (sale is null) return NotFound();
        if (sale.FulfillmentType != FulfillmentType.Deliver)
            return BadRequest(new { message = "Sale is not a delivery." });

        if (request.Status is not (DeliveryStatus.Pending or DeliveryStatus.Delivered or DeliveryStatus.Cancelled))
            return BadRequest(new { message = "Invalid delivery status." });

        sale.DeliveryStatus = request.Status;
        await _db.SaveChangesAsync();

        return Ok(new DeliveryDto(
            sale.Id,
            sale.Customer?.Name,
            sale.DeliveryAddress,
            sale.DeliveryNotes,
            sale.DeliveryStatus,
            sale.Subtotal,
            sale.CreatedAt));
    }
}
