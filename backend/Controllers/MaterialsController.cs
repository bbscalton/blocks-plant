using BlocksPlant.Api.Data;
using BlocksPlant.Api.Dtos;
using BlocksPlant.Api.Models;
using BlocksPlant.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Controllers;

[ApiController]
[Route("api/materials")]
[Authorize]
public class MaterialsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MaterialsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<RawMaterialDto>>> List([FromQuery] bool includeInactive = false)
    {
        var query = _db.RawMaterials.AsQueryable();
        if (!includeInactive)
            query = query.Where(m => m.IsActive);

        var items = await query.OrderBy(m => m.Name).ToListAsync();
        return items.Select(Map).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RawMaterialDto>> Get(int id)
    {
        var material = await _db.RawMaterials.FindAsync(id);
        if (material is null) return NotFound(new { message = "Material not found." });
        return Map(material);
    }

    [HttpPost]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<RawMaterialDto>> Create([FromBody] CreateRawMaterialRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });
        if (string.IsNullOrWhiteSpace(request.Unit))
            return BadRequest(new { message = "Unit is required." });

        var material = new RawMaterial
        {
            Name = request.Name.Trim(),
            Unit = request.Unit.Trim(),
            QuantityOnHand = request.QuantityOnHand,
            MinStock = request.MinStock,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            IsActive = true
        };

        _db.RawMaterials.Add(material);
        await _db.SaveChangesAsync();
        return Ok(Map(material));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<RawMaterialDto>> Update(int id, [FromBody] UpdateRawMaterialRequest request)
    {
        var material = await _db.RawMaterials.FindAsync(id);
        if (material is null) return NotFound(new { message = "Material not found." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });
        if (string.IsNullOrWhiteSpace(request.Unit))
            return BadRequest(new { message = "Unit is required." });

        material.Name = request.Name.Trim();
        material.Unit = request.Unit.Trim();
        material.MinStock = request.MinStock;
        material.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        material.IsActive = request.IsActive;
        await _db.SaveChangesAsync();
        return Ok(Map(material));
    }

    /// <summary>Receive a delivery — increases on-hand quantity.</summary>
    [HttpPost("{id:int}/receive")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<RawMaterialDto>> Receive(int id, [FromBody] MaterialReceiveRequest request)
    {
        var material = await _db.RawMaterials.FindAsync(id);
        if (material is null) return NotFound(new { message = "Material not found." });
        if (request.Quantity <= 0)
            return BadRequest(new { message = "Quantity must be greater than zero." });

        material.QuantityOnHand += request.Quantity;
        _db.MaterialTransactions.Add(new MaterialTransaction
        {
            RawMaterialId = material.Id,
            Type = MaterialTxnType.Receive,
            QuantityDelta = request.Quantity,
            QuantityAfter = material.QuantityOnHand,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = User.GetUserId()
        });
        await _db.SaveChangesAsync();
        return Ok(Map(material));
    }

    /// <summary>Arbitrary adjust (positive or negative). Cannot go below zero.</summary>
    [HttpPost("{id:int}/adjust")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<RawMaterialDto>> Adjust(int id, [FromBody] MaterialAdjustRequest request)
    {
        var material = await _db.RawMaterials.FindAsync(id);
        if (material is null) return NotFound(new { message = "Material not found." });
        if (request.QuantityDelta == 0)
            return BadRequest(new { message = "Quantity delta must be non-zero." });

        var newQty = material.QuantityOnHand + request.QuantityDelta;
        if (newQty < 0)
            return BadRequest(new { message = "Adjustment would make material stock negative." });

        material.QuantityOnHand = newQty;
        _db.MaterialTransactions.Add(new MaterialTransaction
        {
            RawMaterialId = material.Id,
            Type = MaterialTxnType.Adjust,
            QuantityDelta = request.QuantityDelta,
            QuantityAfter = material.QuantityOnHand,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = User.GetUserId()
        });
        await _db.SaveChangesAsync();
        return Ok(Map(material));
    }

    internal static RawMaterialDto Map(RawMaterial m) => new(
        m.Id,
        m.Name,
        m.Unit,
        m.QuantityOnHand,
        m.MinStock,
        m.Notes,
        m.QuantityOnHand < m.MinStock,
        m.IsActive);
}
