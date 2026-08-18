using System.Security.Claims;
using BluePrintHr.Api.Contracts;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Models;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(BluePrintHrDbContext db, IPasswordService passwords) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<UserDto>> Login(LoginRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(x => x.Employee).SingleOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail);
        if (user is null || !passwords.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        user.LastSignedIn = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await SignInAsync(user);
        return Ok(ToDto(user));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        var user = await db.Users.Include(x => x.Employee).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        return user is null ? Unauthorized() : Ok(ToDto(user));
    }

    private async Task SignInAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("tenant_id", user.TenantId.ToString())
        };
        if (user.EmployeeId.HasValue) claims.Add(new Claim("employee_id", user.EmployeeId.Value.ToString()));
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
        });
    }

    private static UserDto ToDto(User user) => new(user.Id, user.Name, user.Email, Label(user.Role), user.TenantId, user.EmployeeId);

    public static string Label(UserRole role) => role switch
    {
        UserRole.SuperAdmin => "Super Admin",
        UserRole.CompanyAdmin => "Company Admin",
        UserRole.HrManager => "HR Manager",
        UserRole.PayrollManager => "Payroll Manager",
        _ => "Employee"
    };
}
