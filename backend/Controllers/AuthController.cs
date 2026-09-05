using BlocksPlant.Api.Data;
using BlocksPlant.Api.Dtos;
using BlocksPlant.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;

    public AuthController(AppDbContext db, JwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password ?? string.Empty, user.PasswordHash))
            return Unauthorized(new { message = "Invalid username or password." });

        var token = _jwt.CreateToken(user);
        return Ok(new LoginResponse(token, user.Username, user.FullName, user.Role.ToString(), user.Id));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "User not found." });

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash))
            return BadRequest(new { message = "Current password is incorrect." });
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return BadRequest(new { message = "New password must be at least 6 characters." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Password changed." });
    }
}
