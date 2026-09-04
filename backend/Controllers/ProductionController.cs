using BlocksPlant.Api.Data;
using BlocksPlant.Api.Dtos;
using BlocksPlant.Api.Models;
using BlocksPlant.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Controllers;

[ApiController]
[Route("api/production")]
[Authorize]
public class ProductionController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductionController(AppDbContext db) => _db = db;

    [HttpPost]
    [Authorize(Roles = "Operator,Owner")]
    public async Task<ActionResult<ProductionDto>> Create([FromBody] ProductionRequest request)
    {
        if (request.Quantity <= 0)
            return BadRequest(new { message = "Quantity must be greater than zero." });
        if (request.Rejects < 0)
            return BadRequest(new { message = "Rejects cannot be negative." });

        if (request.ClientId is Guid clientId && clientId != Guid.Empty)
        {
            var existing = await _db.ProductionEntries
                .Include(p => p.Product)
                .Include(p => p.CreatedByUser)
                .FirstOrDefaultAsync(p => p.ClientId == clientId);
            if (existing is not null)
                return Ok(Map(existing));
        }

        var product = await _db.Products.FindAsync(request.ProductId);
        if (product is null) return NotFound(new { message = "Product not found." });

        var recipeLines = await _db.RecipeLines
            .Include(r => r.RawMaterial)
            .Where(r => r.ProductId == product.Id && r.QuantityPerBlock > 0)
            .ToListAsync();

        // Good qty only consumes materials (rejects do not consume extra).
        var goodQty = request.Quantity;
        var shortages = new List<string>();
        foreach (var line in recipeLines)
        {
            var needed = line.QuantityPerBlock * goodQty;
            var available = line.RawMaterial?.QuantityOnHand ?? 0;
            if (available < needed)
            {
                var name = line.RawMaterial?.Name ?? $"Material #{line.RawMaterialId}";
                var unit = line.RawMaterial?.Unit ?? "";
                shortages.Add($"{name}: need {needed:0.####} {unit}, have {available:0.####} {unit}");
            }
        }

        if (shortages.Count > 0)
        {
            return BadRequest(new
            {
                message = "Insufficient raw materials for production. " + string.Join("; ", shortages)
            });
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        var userId = User.GetUserId();
        var entry = new ProductionEntry
        {
            ProductId = product.Id,
            Quantity = request.Quantity,
            Rejects = request.Rejects,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
            ClientId = request.ClientId is Guid cid && cid != Guid.Empty ? cid : null
        };

        product.Quantity += request.Quantity;
        _db.ProductionEntries.Add(entry);
        await _db.SaveChangesAsync();

        foreach (var line in recipeLines)
        {
            var material = line.RawMaterial!;
            var consume = line.QuantityPerBlock * goodQty;
            material.QuantityOnHand -= consume;
            _db.MaterialTransactions.Add(new MaterialTransaction
            {
                RawMaterialId = material.Id,
                Type = MaterialTxnType.ProductionConsume,
                QuantityDelta = -consume,
                QuantityAfter = material.QuantityOnHand,
                Notes = $"Production of {goodQty} × {product.Name}",
                ProductionEntryId = entry.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId
            });
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        var user = await _db.Users.FindAsync(userId);
        return Ok(new ProductionDto(
            entry.Id,
            product.Id,
            product.Name,
            entry.Quantity,
            entry.Rejects,
            entry.CreatedAt,
            user?.FullName ?? string.Empty,
            entry.ClientId));
    }

    [HttpGet]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<List<ProductionDto>>> List([FromQuery] int take = 50)
    {
        take = Math.Clamp(take, 1, 200);
        var items = await _db.ProductionEntries
            .Include(p => p.Product)
            .Include(p => p.CreatedByUser)
            .OrderByDescending(p => p.CreatedAt)
            .Take(take)
            .ToListAsync();

        return items.Select(Map).ToList();
    }

    private static ProductionDto Map(ProductionEntry e) => new(
        e.Id,
        e.ProductId,
        e.Product?.Name ?? string.Empty,
        e.Quantity,
        e.Rejects,
        e.CreatedAt,
        e.CreatedByUser?.FullName ?? string.Empty,
        e.ClientId);
}
