using BlocksPlant.Api.Data;
using BlocksPlant.Api.Dtos;
using BlocksPlant.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Controllers;

[ApiController]
[Route("api/recipes")]
[Authorize]
public class RecipesController : ControllerBase
{
    private readonly AppDbContext _db;

    public RecipesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<ProductRecipeDto>>> ListAll()
    {
        var products = await _db.Products.OrderBy(p => p.SizeInches).ToListAsync();
        var lines = await _db.RecipeLines
            .Include(r => r.RawMaterial)
            .ToListAsync();

        return products.Select(p => MapProduct(p, lines.Where(l => l.ProductId == p.Id))).ToList();
    }

    [HttpGet("by-product/{productId:int}")]
    public async Task<ActionResult<ProductRecipeDto>> GetByProduct(int productId)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product is null) return NotFound(new { message = "Product not found." });

        var lines = await _db.RecipeLines
            .Include(r => r.RawMaterial)
            .Where(r => r.ProductId == productId)
            .OrderBy(r => r.RawMaterial!.Name)
            .ToListAsync();

        return MapProduct(product, lines);
    }

    [HttpPut("by-product/{productId:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<ProductRecipeDto>> SetRecipe(int productId, [FromBody] SetProductRecipeRequest request)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product is null) return NotFound(new { message = "Product not found." });

        var lines = request.Lines ?? new List<UpsertRecipeLineRequest>();
        if (lines.Any(l => l.QuantityPerBlock < 0))
            return BadRequest(new { message = "Quantity per block cannot be negative." });

        var materialIds = lines.Select(l => l.RawMaterialId).Distinct().ToList();
        if (materialIds.Count != lines.Count)
            return BadRequest(new { message = "Duplicate materials in recipe are not allowed." });

        var materials = await _db.RawMaterials
            .Where(m => materialIds.Contains(m.Id) && m.IsActive)
            .ToDictionaryAsync(m => m.Id);

        foreach (var id in materialIds)
        {
            if (!materials.ContainsKey(id))
                return NotFound(new { message = $"Raw material {id} not found or inactive." });
        }

        var existing = await _db.RecipeLines.Where(r => r.ProductId == productId).ToListAsync();
        _db.RecipeLines.RemoveRange(existing);

        foreach (var line in lines.Where(l => l.QuantityPerBlock > 0))
        {
            _db.RecipeLines.Add(new RecipeLine
            {
                ProductId = productId,
                RawMaterialId = line.RawMaterialId,
                QuantityPerBlock = line.QuantityPerBlock
            });
        }

        await _db.SaveChangesAsync();

        var saved = await _db.RecipeLines
            .Include(r => r.RawMaterial)
            .Where(r => r.ProductId == productId)
            .OrderBy(r => r.RawMaterial!.Name)
            .ToListAsync();

        return Ok(MapProduct(product, saved));
    }

    private static ProductRecipeDto MapProduct(Product product, IEnumerable<RecipeLine> lines) => new(
        product.Id,
        product.Name,
        product.SizeInches,
        lines.Select(l => new RecipeLineDto(
            l.Id,
            l.ProductId,
            l.RawMaterialId,
            l.RawMaterial?.Name ?? string.Empty,
            l.RawMaterial?.Unit ?? string.Empty,
            l.QuantityPerBlock)).ToList());
}
