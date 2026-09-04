using BlocksPlant.Api.Data;
using BlocksPlant.Api.Dtos;
using BlocksPlant.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> GetAll()
    {
        var hidePrices = User.IsOperator();
        var products = await _db.Products.OrderBy(p => p.SizeInches).ToListAsync();
        return products.Select(p => new ProductDto(
            p.Id,
            p.Name,
            p.SizeInches,
            hidePrices ? null : p.PricePerBlock,
            hidePrices ? 0 : p.MinStock,
            p.Quantity)).ToList();
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<ProductDto>> Update(int id, [FromBody] UpdateProductRequest request)
    {
        if (request.PricePerBlock < 0 || request.MinStock < 0)
            return BadRequest(new { message = "Price and min stock must be non-negative." });

        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();

        product.PricePerBlock = request.PricePerBlock;
        product.MinStock = request.MinStock;
        await _db.SaveChangesAsync();

        return Ok(new ProductDto(product.Id, product.Name, product.SizeInches, product.PricePerBlock, product.MinStock, product.Quantity));
    }
}
