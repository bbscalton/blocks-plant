using BlocksPlant.Api.Data;
using BlocksPlant.Api.Dtos;
using BlocksPlant.Api.Models;
using BlocksPlant.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Owner")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> List()
    {
        var users = await _db.Users.OrderBy(u => u.Username).ToListAsync();
        return users.Select(Map).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest(new { message = "Username is required." });
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return BadRequest(new { message = "Password must be at least 6 characters." });
        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "Full name is required." });
        if (!TryParseRole(request.Role, out var role))
            return BadRequest(new { message = "Role must be Owner, Cashier, or Operator." });

        if (await _db.Users.AnyAsync(u => u.Username == username))
            return BadRequest(new { message = "Username already exists." });

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            Role = role,
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(Map(user));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> Update(int id, [FromBody] UpdateUserRequest request)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound(new { message = "User not found." });

        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "Full name is required." });
        if (!TryParseRole(request.Role, out var role))
            return BadRequest(new { message = "Role must be Owner, Cashier, or Operator." });

        if (!request.IsActive || role != UserRole.Owner)
        {
            var wouldRemoveLastOwner = user.Role == UserRole.Owner && user.IsActive
                && (!request.IsActive || role != UserRole.Owner)
                && !await _db.Users.AnyAsync(u => u.Id != id && u.Role == UserRole.Owner && u.IsActive);
            if (wouldRemoveLastOwner)
                return BadRequest(new { message = "Cannot deactivate or demote the last active Owner." });
        }

        user.FullName = request.FullName.Trim();
        user.Role = role;
        user.IsActive = request.IsActive;
        await _db.SaveChangesAsync();
        return Ok(Map(user));
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound(new { message = "User not found." });
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return BadRequest(new { message = "Password must be at least 6 characters." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Password reset." });
    }

    [HttpPost("{id:int}/deactivate")]
    public async Task<ActionResult<UserDto>> Deactivate(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound(new { message = "User not found." });

        if (user.Role == UserRole.Owner && user.IsActive)
        {
            var otherOwners = await _db.Users.CountAsync(u => u.Id != id && u.Role == UserRole.Owner && u.IsActive);
            if (otherOwners == 0)
                return BadRequest(new { message = "Cannot deactivate the last active Owner." });
        }

        user.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(Map(user));
    }

    private static bool TryParseRole(string? role, out UserRole parsed)
    {
        parsed = UserRole.Cashier;
        if (string.IsNullOrWhiteSpace(role)) return false;
        return Enum.TryParse(role.Trim(), ignoreCase: true, out parsed)
               && Enum.IsDefined(parsed);
    }

    private static UserDto Map(User u) =>
        new(u.Id, u.Username, u.FullName, u.Role.ToString(), u.IsActive);
}
