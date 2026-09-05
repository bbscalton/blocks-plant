using BlocksPlant.Api.Data;
using BlocksPlant.Api.Dtos;
using BlocksPlant.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private static readonly HashSet<string> AllowedLogoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp"
    };

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public SettingsController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PlantSettingsDto>> Get()
    {
        var settings = await GetOrCreateAsync();
        return Ok(Map(settings));
    }

    [HttpPut]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<PlantSettingsDto>> Update([FromBody] UpdatePlantSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BusinessName))
            return BadRequest(new { message = "Business name is required." });

        var settings = await GetOrCreateAsync();
        settings.BusinessName = request.BusinessName.Trim();
        settings.Address = NullIfBlank(request.Address);
        settings.Phone = NullIfBlank(request.Phone);
        settings.Email = NullIfBlank(request.Email);
        settings.TaxId = NullIfBlank(request.TaxId);
        settings.ReceiptFooter = NullIfBlank(request.ReceiptFooter) ?? "Thank you!";
        settings.ReceiptShowLogo = request.ReceiptShowLogo;
        settings.ReceiptShowStoreName = request.ReceiptShowStoreName;
        settings.ReceiptShowAddress = request.ReceiptShowAddress;
        settings.ReceiptShowPhone = request.ReceiptShowPhone;
        settings.ReceiptShowCashier = request.ReceiptShowCashier;
        settings.ReceiptShowThankYou = request.ReceiptShowThankYou;
        await _db.SaveChangesAsync();
        return Ok(Map(settings));
    }

    [HttpPost("logo")]
    [Authorize(Roles = "Owner")]
    [RequestSizeLimit(2_000_000)]
    public async Task<ActionResult<PlantSettingsDto>> UploadLogo(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Logo file is required." });
        if (file.Length > 2_000_000)
            return BadRequest(new { message = "Logo must be 2 MB or smaller." });

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedLogoExtensions.Contains(ext))
            return BadRequest(new { message = "Logo must be PNG, JPG, GIF, or WebP." });

        var settings = await GetOrCreateAsync();
        var logosDir = GetLogosDirectory();
        Directory.CreateDirectory(logosDir);

        // Remove previous logo file if present
        if (!string.IsNullOrWhiteSpace(settings.LogoFileName))
        {
            var oldPath = Path.Combine(logosDir, settings.LogoFileName);
            if (System.IO.File.Exists(oldPath))
                System.IO.File.Delete(oldPath);
        }

        var fileName = $"logo{ext.ToLowerInvariant()}";
        var path = Path.Combine(logosDir, fileName);
        await using (var stream = System.IO.File.Create(path))
            await file.CopyToAsync(stream);

        settings.LogoFileName = fileName;
        await _db.SaveChangesAsync();
        return Ok(Map(settings));
    }

    [HttpDelete("logo")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<PlantSettingsDto>> ClearLogo()
    {
        var settings = await GetOrCreateAsync();
        if (!string.IsNullOrWhiteSpace(settings.LogoFileName))
        {
            var path = Path.Combine(GetLogosDirectory(), settings.LogoFileName);
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
            settings.LogoFileName = null;
            await _db.SaveChangesAsync();
        }

        return Ok(Map(settings));
    }

    [HttpGet("logo")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLogo()
    {
        var settings = await GetOrCreateAsync();
        if (string.IsNullOrWhiteSpace(settings.LogoFileName))
            return NotFound(new { message = "No logo uploaded." });

        var path = Path.Combine(GetLogosDirectory(), settings.LogoFileName);
        if (!System.IO.File.Exists(path))
            return NotFound(new { message = "Logo file missing." });

        var contentType = settings.LogoFileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
            : settings.LogoFileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? "image/gif"
            : settings.LogoFileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
            : "image/jpeg";
        return PhysicalFile(path, contentType);
    }

    private async Task<PlantSettings> GetOrCreateAsync()
    {
        var settings = await _db.PlantSettings.FirstOrDefaultAsync(s => s.Id == 1);
        if (settings is not null) return settings;

        settings = new PlantSettings { Id = 1, BusinessName = "Blocks Plant", ReceiptFooter = "Thank you!" };
        _db.PlantSettings.Add(settings);
        await _db.SaveChangesAsync();
        return settings;
    }

    private string GetLogosDirectory() =>
        Path.Combine(_env.ContentRootPath, "App_Data", "logos");

    private PlantSettingsDto Map(PlantSettings s) => new(
        s.BusinessName,
        s.Address,
        s.Phone,
        s.Email,
        s.TaxId,
        s.ReceiptFooter,
        !string.IsNullOrWhiteSpace(s.LogoFileName),
        string.IsNullOrWhiteSpace(s.LogoFileName) ? null : "/api/settings/logo",
        s.ReceiptShowLogo,
        s.ReceiptShowStoreName,
        s.ReceiptShowAddress,
        s.ReceiptShowPhone,
        s.ReceiptShowCashier,
        s.ReceiptShowThankYou);

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
