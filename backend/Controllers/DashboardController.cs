using BlocksPlant.Api.Data;
using BlocksPlant.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "Owner")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get()
    {
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);

        var stock = await _db.Products.Where(p => p.IsActive).OrderBy(p => p.SizeInches).ToListAsync();
        var stockDtos = stock.Select(p => new StockDto(
            p.Id, p.Name, p.SizeInches, p.Quantity, p.MinStock, p.Quantity < p.MinStock)).ToList();

        var materials = await _db.RawMaterials
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .ToListAsync();
        var materialDtos = materials.Select(MaterialsController.Map).ToList();

        var todayProductionQty = await _db.ProductionEntries
            .Where(p => p.CreatedAt >= todayStart && p.CreatedAt < todayEnd)
            .SumAsync(p => (int?)p.Quantity) ?? 0;

        var todaySales = await _db.Sales
            .Where(s => s.CreatedAt >= todayStart && s.CreatedAt < todayEnd)
            .ToListAsync();

        var debtors = await _db.Customers.Where(c => c.Balance > 0).ToListAsync();

        return Ok(new DashboardDto(
            stockDtos,
            todayProductionQty,
            todaySales.Sum(s => s.Subtotal),
            todaySales.Count,
            debtors.Sum(c => c.Balance),
            debtors.Count,
            materialDtos));
    }
}
