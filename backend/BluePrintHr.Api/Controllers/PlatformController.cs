using System.Security.Cryptography;
using BluePrintHr.Api.Contracts;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Models;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/platform")]
[Authorize(Roles = nameof(UserRole.SuperAdmin))]
public class PlatformController(BluePrintHrDbContext db, IPasswordService passwords, IEmailService email, IRequestContext context) : ControllerBase
{
    [HttpGet("tenants")]
    public async Task<ActionResult<IReadOnlyList<TenantAdminDto>>> Tenants()
    {
        var tenants = await db.Tenants.AsNoTracking().OrderBy(x => x.CompanyName).ToListAsync();
        var users = await db.Users.AsNoTracking().GroupBy(x => x.TenantId).Select(g => new { TenantId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.TenantId, x => x.Count);
        var employees = await db.Employees.AsNoTracking().GroupBy(x => x.TenantId).Select(g => new { TenantId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.TenantId, x => x.Count);
        return Ok(tenants.Select(t => new TenantAdminDto(t.Id, t.CompanyName, t.Status.ToString(), users.GetValueOrDefault(t.Id), employees.GetValueOrDefault(t.Id))).ToList());
    }

    [HttpPost("tenants")]
    public async Task<ActionResult<TenantAdminDto>> CreateTenant(CreateTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName)) return BadRequest(new { message = "Company name is required." });
        var subdomain = string.IsNullOrWhiteSpace(request.Subdomain) ? null : request.Subdomain.Trim().ToLowerInvariant();
        if (subdomain is not null && await db.Tenants.AnyAsync(x => x.Subdomain == subdomain))
            return Conflict(new { message = "Subdomain is already in use." });
        var tenant = new Tenant { CompanyName = request.CompanyName.Trim(), KraPin = request.KraPin?.Trim().ToUpperInvariant(), Email = request.Email?.Trim(), Phone = request.Phone?.Trim(), Address = request.Address?.Trim(), Subdomain = subdomain, Status = TenantStatus.Active };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        db.AuditLogs.Add(new AuditLog { TenantId = tenant.Id, UserId = context.UserId, UserName = User.Identity?.Name, Action = "CREATE", EntityType = "Tenant", EntityId = tenant.Id, Details = $"Tenant {tenant.CompanyName} created." });
        await db.SaveChangesAsync();
        return Ok(new TenantAdminDto(tenant.Id, tenant.CompanyName, tenant.Status.ToString(), 0, 0));
    }

    [HttpPut("tenants/{id:int}")]
    public async Task<ActionResult<TenantAdminDto>> UpdateTenant(int id, UpdateTenantRequest request)
    {
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Id == id);
        if (tenant is null) return NotFound();
        var subdomain = string.IsNullOrWhiteSpace(request.Subdomain) ? null : request.Subdomain.Trim().ToLowerInvariant();
        if (subdomain is not null && await db.Tenants.AnyAsync(x => x.Id != id && x.Subdomain == subdomain))
            return Conflict(new { message = "Subdomain is already in use." });
        tenant.CompanyName = request.CompanyName.Trim();
        tenant.KraPin = request.KraPin?.Trim().ToUpperInvariant();
        tenant.Email = request.Email?.Trim();
        tenant.Phone = request.Phone?.Trim();
        tenant.Address = request.Address?.Trim();
        tenant.Subdomain = subdomain;
        tenant.Status = request.Status;
        await db.SaveChangesAsync();
        if (tenant.Status == TenantStatus.Suspended)
        {
            var users = await db.Users.Where(x => x.TenantId == id && x.Active).ToListAsync();
            foreach (var user in users) { user.Active = false; user.SecurityStamp = Guid.NewGuid().ToString("N"); }
            await db.SaveChangesAsync();
        }
        return Ok(new TenantAdminDto(tenant.Id, tenant.CompanyName, tenant.Status.ToString(),
            await db.Users.CountAsync(x => x.TenantId == id), await db.Employees.CountAsync(x => x.TenantId == id)));
    }

    [HttpGet("tenants/{tenantId:int}/users")]
    public async Task<IActionResult> Users(int tenantId)
    {
        var users = await db.Users.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Email, Role = x.Role.ToString(), x.Active, x.EmployeeId, x.LastSignedIn }).ToListAsync();
        return Ok(users);
    }

    [HttpPost("tenants/{tenantId:int}/admins")]
    public async Task<IActionResult> CreateTenantAdmin(int tenantId, CreatePlatformAdminRequest request)
    {
        if (!await db.Tenants.AnyAsync(x => x.Id == tenantId)) return NotFound(new { message = "Tenant not found." });
        if (request.Role is UserRole.SuperAdmin or UserRole.Employee) return BadRequest(new { message = "Use a tenant management role for this account." });
        if (await db.Users.AnyAsync(x => x.TenantId == tenantId && x.Email.ToLower() == request.Email.Trim().ToLowerInvariant()))
            return Conflict(new { message = "Email already belongs to a user in this tenant." });

        var user = new User
        {
            TenantId = tenantId, Name = request.Name.Trim(), Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = passwords.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))),
            Role = request.Role, Active = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+','-').Replace('/','_');
        var tokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
        db.PasswordResetTokens.Add(new PasswordResetToken { UserId = user.Id, TokenHash = tokenHash, ExpiresAt = DateTime.UtcNow.AddHours(24), RequestedIp = HttpContext.Connection.RemoteIpAddress?.ToString() });
        await db.SaveChangesAsync();
        var frontend = (HttpContext.RequestServices.GetRequiredService<IConfiguration>()["App:FrontendUrl"] ?? "").TrimEnd('/');
        var link = string.IsNullOrWhiteSpace(frontend) ? rawToken : $"{frontend}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        await email.SendAsync(user.Email, "Your BluePrint HR administrator account", $"<p>Your tenant administrator account has been created.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Set your password</a></p>");
        return Ok(new { user.Id, user.Email, role = user.Role.ToString(), user.Active });
    }

    [HttpPost("users/{userId:int}/deactivate")]
    public async Task<IActionResult> DeactivatePlatformUser(int userId)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return NotFound();
        if (user.Id == context.UserId) return BadRequest(new { message = "You cannot deactivate your own account." });
        if (user.Role == UserRole.SuperAdmin && await db.Users.CountAsync(x => x.Role == UserRole.SuperAdmin && x.Active) <= 1)
            return Conflict(new { message = "The last active Super Admin cannot be deactivated." });
        user.Active = false;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
        return NoContent();
    }
}
