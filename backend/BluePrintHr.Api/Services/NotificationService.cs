using BluePrintHr.Api.Data;
using BluePrintHr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Services;

public interface INotificationService
{
    Task NotifyUserAsync(int userId, string type, string title, string message, string? entityType = null, int? entityId = null);
    Task NotifyEmployeeAsync(int tenantId, int employeeId, string type, string title, string message, string? entityType = null, int? entityId = null);
}

public sealed class NotificationService(BluePrintHrDbContext db, IEmailService email, ILogger<NotificationService> logger) : INotificationService
{
    public async Task NotifyUserAsync(int userId, string type, string title, string message, string? entityType = null, int? entityId = null)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return;
        db.Notifications.Add(new Notification { TenantId = user.TenantId, UserId = userId, Type = type, Title = title, Message = message, EntityType = entityType, EntityId = entityId });
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            try { await email.SendAsync(user.Email, title, $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>"); }
            catch (Exception ex) { logger.LogError(ex, "Email notification failed for user {UserId}", userId); }
        }
    }

    public async Task NotifyEmployeeAsync(int tenantId, int employeeId, string type, string title, string message, string? entityType = null, int? entityId = null)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.EmployeeId == employeeId);
        if (user is null) return;
        db.Notifications.Add(new Notification { TenantId = tenantId, UserId = user.Id, Type = type, Title = title, Message = message, EntityType = entityType, EntityId = entityId });
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            try { await email.SendAsync(user.Email, title, $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>"); }
            catch (Exception ex) { logger.LogError(ex, "Email notification failed for employee user {UserId}", user.Id); }
        }
    }
}
