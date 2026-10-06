using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
[Route("api/accounts")]
[Authorize]
public class AccountsController(BluePrintHrDbContext db, IRequestContext context, IPasswordService passwords, IEmailService email) : ControllerBase
{
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == context.UserId && x.TenantId == context.TenantId);
        if (user is null) return Unauthorized();
        if (!passwords.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Current password is incorrect." });
        var validation = ValidatePassword(request.NewPassword);
        if (validation is not null) return BadRequest(new { message = validation });
        if (passwords.Verify(request.NewPassword, user.PasswordHash))
            return BadRequest(new { message = "New password must be different from the current password." });

        user.PasswordHash = passwords.Hash(request.NewPassword);
        user.SecurityStamp = NewStamp();
        await db.SaveChangesAsync();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var emailAddress = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(x => x.Tenant).SingleOrDefaultAsync(x => x.Email.ToLower() == emailAddress);
        if (user is not null && user.Active && user.Tenant.Status != TenantStatus.Suspended)
        {
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+','-').Replace('/','_');
            var hash = HashToken(rawToken);
            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = hash,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                RequestedIp = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await db.SaveChangesAsync();

            var frontend = (HttpContext.RequestServices.GetRequiredService<IConfiguration>()["App:FrontendUrl"] ?? "").TrimEnd('/');
            var link = string.IsNullOrWhiteSpace(frontend) ? rawToken : $"{frontend}/reset-password?token={Uri.EscapeDataString(rawToken)}";
            await email.SendAsync(user.Email, "BluePrint HR password reset", $"<p>A password reset was requested for your BluePrint HR account.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Reset your password</a></p><p>This link expires in 30 minutes. If you did not request it, you can ignore this email.</p>");
        }
        return Ok(new { message = "If an active account exists for that email, password reset instructions have been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var validation = ValidatePassword(request.NewPassword);
        if (validation is not null) return BadRequest(new { message = validation });
        var hash = HashToken(request.Token);
        var row = await db.PasswordResetTokens.Include(x => x.User).ThenInclude(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.TokenHash == hash && x.UsedAt == null && x.ExpiresAt > DateTime.UtcNow);
        if (row is null || !row.User.Active || row.User.Tenant.Status == TenantStatus.Suspended)
            return BadRequest(new { message = "This reset link is invalid or has expired." });

        row.User.PasswordHash = passwords.Hash(request.NewPassword);
        row.User.SecurityStamp = NewStamp();
        row.UsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }


    [HttpGet("users")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<IActionResult> Users()
    {
        var users = await db.Users.AsNoTracking().Where(x => x.TenantId == context.TenantId).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Email, Role = x.Role.ToString(), x.Active, x.EmployeeId, x.LastSignedIn }).ToListAsync();
        return Ok(users);
    }

    [HttpPost("employees/{employeeId:int}/activate")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<AccountStatusDto>> ActivateEmployee(int employeeId, ActivateEmployeeAccountRequest request)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == employeeId && x.TenantId == context.TenantId);
        if (employee is null) return NotFound(new { message = "Employee not found." });
        if (!string.Equals(employee.EmploymentStatus, "Active", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = "Only active employees can have active accounts." });

        var user = await db.Users.SingleOrDefaultAsync(x => x.TenantId == context.TenantId && x.EmployeeId == employeeId);
        var created = false;
        if (user is null)
        {
            var accountEmail = string.IsNullOrWhiteSpace(request.Email) ? employee.Email : request.Email;
            if (string.IsNullOrWhiteSpace(accountEmail))
                return BadRequest(new { message = "An email address is required to activate an employee account." });
            if (await db.Users.AnyAsync(x => x.TenantId == context.TenantId && x.Email.ToLower() == accountEmail.Trim().ToLowerInvariant()))
                return Conflict(new { message = "That email is already assigned to another account." });

            user = new User
            {
                TenantId = context.TenantId,
                EmployeeId = employee.Id,
                Name = $"{employee.FirstName} {employee.LastName}".Trim(),
                Email = accountEmail.Trim().ToLowerInvariant(),
                PasswordHash = passwords.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))),
                Role = UserRole.Employee,
                Active = true,
                SecurityStamp = NewStamp()
            };
            db.Users.Add(user);
            created = true;
        }
        else
        {
            user.Active = true;
            user.SecurityStamp = NewStamp();
        }

        await db.SaveChangesAsync();
        if (created || user.Email == employee.Email)
        {
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+','-').Replace('/','_');
            db.PasswordResetTokens.Add(new PasswordResetToken { UserId = user.Id, TokenHash = HashToken(rawToken), ExpiresAt = DateTime.UtcNow.AddHours(24), RequestedIp = HttpContext.Connection.RemoteIpAddress?.ToString() });
            await db.SaveChangesAsync();
            var frontend = (HttpContext.RequestServices.GetRequiredService<IConfiguration>()["App:FrontendUrl"] ?? "").TrimEnd('/');
            var link = string.IsNullOrWhiteSpace(frontend) ? rawToken : $"{frontend}/reset-password?token={Uri.EscapeDataString(rawToken)}";
            await email.SendAsync(user.Email, "Your BluePrint HR account", $"<p>Your BluePrint HR employee account is now active.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Set your password</a></p><p>This link expires in 24 hours.</p>");
        }
        return Ok(new AccountStatusDto(user.Id, user.EmployeeId, user.Email, user.Active));
    }

    [HttpPost("users/{userId:int}/deactivate")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<IActionResult> DeactivateUser(int userId)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.TenantId == context.TenantId);
        if (user is null) return NotFound();
        if (user.Id == context.UserId) return BadRequest(new { message = "You cannot deactivate your own account." });
        user.Active = false;
        user.SecurityStamp = NewStamp();
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("users/{userId:int}/activate")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<IActionResult> ActivateUser(int userId)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.TenantId == context.TenantId);
        if (user is null) return NotFound();
        user.Active = true;
        user.SecurityStamp = NewStamp();
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string? ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12) return "Password must be at least 12 characters.";
        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || !password.Any(ch => !char.IsLetterOrDigit(ch)))
            return "Password must include upper-case, lower-case, number and special character.";
        return null;
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N");
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
