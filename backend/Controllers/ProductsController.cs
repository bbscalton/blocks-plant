using BlocksPlant.Api.Data;
using BlocksPlant.Api.Dtos;
using BlocksPlant.Api.Models;
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
    public async Task<ActionResult<List<ProductDto>>> GetAll([FromQuery] bool includeInactive = false)
    {
        var hidePrices = User.IsOperator();
        var query = _db.Products.AsQueryable();
        if (!includeInactive)
            query = query.Where(p => p.IsActive);

        var products = await query.OrderBy(p => p.SizeInches).ThenBy(p => p.Name).ToListAsync();
        return products.Select(p => Map(p, hidePrices)).ToList();
    }

    [HttpPost]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });
        if (request.PricePerBlock < 0 || request.MinStock < 0 || request.Quantity < 0)
            return BadRequest(new { message = "Price, min stock, and quantity must be non-negative." });

        var product = new Product
        {
            Name = request.Name.Trim(),
            SizeInches = request.SizeInches,
            PricePerBlock = request.PricePerBlock,
            MinStock = request.MinStock,
            Quantity = request.Quantity,
            IsActive = true
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return Ok(Map(product, hidePrices: false));
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

        return Ok(Map(product, hidePrices: false));
    }

    /// <summary>
    /// Hard-deletes unused products; otherwise soft-deactivates so sales history stays intact.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<DeleteProductResult>> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound(new { message = "Product not found." });

        var usedInSales = await _db.SaleLines.AnyAsync(l => l.ProductId == id);
        var usedInProduction = await _db.ProductionEntries.AnyAsync(e => e.ProductId == id);
        var usedInRecipes = await _db.RecipeLines.AnyAsync(r => r.ProductId == id);

        if (usedInSales || usedInProduction || usedInRecipes)
        {
            product.IsActive = false;
            await _db.SaveChangesAsync();
            return Ok(new DeleteProductResult(
                HardDeleted: false,
                Deactivated: true,
                Message: "Product has sales/production history — deactivated instead of deleted."));
        }

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return Ok(new DeleteProductResult(
            HardDeleted: true,
            Deactivated: false,
            Message: "Product deleted."));
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<ProductDto>> Deactivate(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound(new { message = "Product not found." });
        product.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(Map(product, hidePrices: false));
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<ProductDto>> Activate(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound(new { message = "Product not found." });
        product.IsActive = true;
        await _db.SaveChangesAsync();
        return Ok(Map(product, hidePrices: false));
    }

    private static ProductDto Map(Product p, bool hidePrices) => new(
        p.Id,
        p.Name,
        p.SizeInches,
        hidePrices ? null : p.PricePerBlock,
        hidePrices ? 0 : p.MinStock,
        p.Quantity,
        p.IsActive);
}
