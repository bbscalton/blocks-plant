using BlocksPlant.Api.Data;
using BlocksPlant.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Controllers;

[ApiController]
[Route("api/stock")]
[Authorize]
public class StockController : ControllerBase
{
    private readonly AppDbContext _db;

    public StockController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<StockDto>>> GetStock()
    {
        var items = await _db.Products.Where(p => p.IsActive).OrderBy(p => p.SizeInches).ToListAsync();
        return items.Select(p => new StockDto(
            p.Id,
            p.Name,
            p.SizeInches,
            p.Quantity,
            p.MinStock,
            p.Quantity < p.MinStock)).ToList();
    }

    [HttpPost("adjust")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<StockDto>> Adjust([FromBody] StockAdjustRequest request)
    {
        var product = await _db.Products.FindAsync(request.ProductId);
        if (product is null) return NotFound(new { message = "Product not found." });

        var newQty = product.Quantity + request.QuantityDelta;
        if (newQty < 0)
            return BadRequest(new { message = "Adjustment would make stock negative." });

        product.Quantity = newQty;
        await _db.SaveChangesAsync();

        return Ok(new StockDto(product.Id, product.Name, product.SizeInches, product.Quantity, product.MinStock, product.Quantity < product.MinStock));
    }
}
