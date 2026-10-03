using BluePrintHr.Api.Contracts;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(BluePrintHrDbContext db, IRequestContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> List([FromQuery] bool unreadOnly = false)
    {
        var query = db.Notifications.AsNoTracking().Where(x => x.TenantId == context.TenantId && x.UserId == context.UserId);
        if (unreadOnly) query = query.Where(x => !x.IsRead);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(50).ToListAsync();
        return Ok(rows.Select(x => new NotificationDto(x.Id, x.Type, x.Title, x.Message, x.EntityType, x.EntityId, x.IsRead, x.CreatedAt)).ToList());
    }

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var row = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId && x.UserId == context.UserId);
        if (row is null) return NotFound();
        row.IsRead = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var rows = await db.Notifications.Where(x => x.TenantId == context.TenantId && x.UserId == context.UserId && !x.IsRead).ToListAsync();
        foreach (var row in rows) row.IsRead = true;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
