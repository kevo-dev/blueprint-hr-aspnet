using System.Data;
using BluePrintHr.Api.Contracts;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Models;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/leave")]
[Authorize]
public class LeaveController(BluePrintHrDbContext db, IRequestContext context, INotificationService notifications) : ControllerBase
{
    [HttpGet("types")]
    public async Task<ActionResult<IReadOnlyList<LeaveTypeDto>>> Types()
    {
        var types = await db.LeaveTypes.AsNoTracking()
            .Where(x => x.TenantId == context.TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync();

        return Ok(types.Select(x => new LeaveTypeDto(x.Id, x.Name, x.DefaultDays, x.Paid, x.Description)).ToList());
    }

    [HttpPost("types")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<LeaveTypeDto>> CreateType(CreateLeaveTypeRequest request)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || request.DefaultDays <= 0)
            return BadRequest(new { message = "Leave type name and positive default days are required." });
        if (await db.LeaveTypes.AnyAsync(x => x.TenantId == context.TenantId && x.Name == name))
            return Conflict(new { message = "Leave type already exists." });

        var type = new LeaveType { TenantId = context.TenantId, Name = name, DefaultDays = request.DefaultDays, Paid = request.Paid, Description = request.Description?.Trim() };
        db.LeaveTypes.Add(type);
        await db.SaveChangesAsync();
        await AuditAsync("CREATE", "LeaveType", type.Id, type.Name);
        return Ok(new LeaveTypeDto(type.Id, type.Name, type.DefaultDays, type.Paid, type.Description));
    }

    [HttpPut("types/{id:int}")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<LeaveTypeDto>> UpdateType(int id, UpdateLeaveTypeRequest request)
    {
        var type = await db.LeaveTypes.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (type is null) return NotFound();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || request.DefaultDays <= 0)
            return BadRequest(new { message = "Leave type name and positive default days are required." });
        if (await db.LeaveTypes.AnyAsync(x => x.TenantId == context.TenantId && x.Name == name && x.Id != id))
            return Conflict(new { message = "Leave type already exists." });

        type.Name = name; type.DefaultDays = request.DefaultDays; type.Paid = request.Paid; type.Description = request.Description?.Trim();
        await db.SaveChangesAsync();
        await AuditAsync("UPDATE", "LeaveType", id, type.Name);
        return Ok(new LeaveTypeDto(type.Id, type.Name, type.DefaultDays, type.Paid, type.Description));
    }

    [HttpDelete("types/{id:int}")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<IActionResult> DeleteType(int id)
    {
        var type = await db.LeaveTypes.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (type is null) return NotFound();
        if (await db.LeaveBalances.AnyAsync(x => x.TenantId == context.TenantId && x.LeaveTypeId == id) ||
            await db.LeaveRequests.AnyAsync(x => x.TenantId == context.TenantId && x.LeaveTypeId == id))
            return Conflict(new { message = "Leave type is in use and cannot be deleted." });

        db.LeaveTypes.Remove(type);
        await db.SaveChangesAsync();
        await AuditAsync("DELETE", "LeaveType", id, type.Name);
        return NoContent();
    }

    [HttpGet("balances")]
    public async Task<ActionResult<IReadOnlyList<LeaveBalanceDto>>> Balances([FromQuery] int? employeeId = null)
    {
        var effectiveEmployeeId = context.CanManageEmployees ? employeeId : context.EmployeeId;
        var query = db.LeaveBalances.AsNoTracking().Where(x => x.TenantId == context.TenantId);

        if (effectiveEmployeeId.HasValue)
            query = query.Where(x => x.EmployeeId == effectiveEmployeeId.Value);

        var balances = await query
            .Join(db.LeaveTypes, balance => balance.LeaveTypeId, type => type.Id,
                (balance, type) => new LeaveBalanceDto(
                    balance.Id, balance.EmployeeId, balance.LeaveTypeId, type.Name, balance.Year,
                    balance.AllocatedDays, balance.UsedDays,
                    Math.Max(balance.AllocatedDays - balance.UsedDays, 0)))
            .ToListAsync();

        return Ok(balances);
    }

    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> Requests([FromQuery] int? employeeId = null)
    {
        var effectiveEmployeeId = context.CanManageEmployees ? employeeId : context.EmployeeId;
        var query = db.LeaveRequests.AsNoTracking().Where(x => x.TenantId == context.TenantId);

        if (effectiveEmployeeId.HasValue)
            query = query.Where(x => x.EmployeeId == effectiveEmployeeId.Value);

        var requests = await query
            .Join(db.Employees, request => request.EmployeeId, employee => employee.Id,
                (request, employee) => new { request, employee })
            .Join(db.LeaveTypes, item => item.request.LeaveTypeId, type => type.Id,
                (item, type) => new LeaveRequestDto(
                    item.request.Id, item.request.EmployeeId,
                    $"{item.employee.FirstName} {item.employee.LastName}",
                    item.request.LeaveTypeId, type.Name, item.request.StartDate,
                    item.request.EndDate, item.request.DaysRequested, item.request.Reason,
                    item.request.Status.ToString(), item.request.CreatedAt))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(requests);
    }

    [HttpPost("requests")]
    public async Task<ActionResult<LeaveRequestDto>> CreateRequest(CreateLeaveRequest request)
    {
        var employeeId = context.CanManageEmployees ? request.EmployeeId : context.EmployeeId;
        if (!employeeId.HasValue)
            return BadRequest(new { message = "No employee profile is linked to this user." });

        if (request.EndDate.Date < request.StartDate.Date || request.DaysRequested <= 0)
            return BadRequest(new { message = "Leave dates and requested days are invalid." });

        var employeeExists = await db.Employees.AnyAsync(x =>
            x.Id == employeeId.Value && x.TenantId == context.TenantId);
        var leaveType = await db.LeaveTypes.SingleOrDefaultAsync(x =>
            x.Id == request.LeaveTypeId && x.TenantId == context.TenantId);

        if (!employeeExists || leaveType is null)
            return BadRequest(new { message = "Employee or leave type is outside the current tenant." });

        var overlaps = await db.LeaveRequests.AnyAsync(x =>
            x.TenantId == context.TenantId &&
            x.EmployeeId == employeeId.Value &&
            (x.Status == LeaveRequestStatus.Pending || x.Status == LeaveRequestStatus.Approved) &&
            x.StartDate <= request.EndDate.Date &&
            x.EndDate >= request.StartDate.Date);

        if (overlaps)
            return Conflict(new { message = "The requested dates overlap an existing pending or approved leave request." });

        var leave = new LeaveRequest
        {
            TenantId = context.TenantId,
            EmployeeId = employeeId.Value,
            LeaveTypeId = request.LeaveTypeId,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            DaysRequested = request.DaysRequested,
            Reason = request.Reason,
            Status = LeaveRequestStatus.Pending
        };

        db.LeaveRequests.Add(leave);
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = context.TenantId,
            UserId = context.UserId,
            UserName = User.Identity?.Name,
            Action = "CREATE",
            EntityType = "LeaveRequest",
            Details = $"Submitted {request.DaysRequested} days."
        });

        var approverIds = await db.Users.AsNoTracking()
            .Where(x => x.TenantId == context.TenantId &&
                (x.Role == UserRole.SuperAdmin || x.Role == UserRole.CompanyAdmin || x.Role == UserRole.HrManager || x.Role == UserRole.PayrollManager))
            .Select(x => x.Id).ToListAsync();
        foreach (var approverId in approverIds)
            await notifications.NotifyUserAsync(approverId, "Leave", "New leave request", $"A new leave request for {request.DaysRequested:0.##} days is awaiting review.", "LeaveRequest", leave.Id);
        await db.SaveChangesAsync();

        return Ok(new LeaveRequestDto(
            leave.Id, leave.EmployeeId, "", leave.LeaveTypeId, leaveType.Name,
            leave.StartDate, leave.EndDate, leave.DaysRequested, leave.Reason,
            leave.Status.ToString(), leave.CreatedAt));
    }

    [HttpPatch("requests/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var leave = await db.LeaveRequests.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (leave is null) return NotFound();

        if (!context.CanManageEmployees && leave.EmployeeId != context.EmployeeId)
            return Forbid();

        if (leave.Status is LeaveRequestStatus.Rejected or LeaveRequestStatus.Cancelled)
            return Conflict(new { message = "This leave request is already closed." });

        if (leave.Status == LeaveRequestStatus.Approved)
        {
            var balance = await db.LeaveBalances.SingleOrDefaultAsync(x =>
                x.TenantId == context.TenantId &&
                x.EmployeeId == leave.EmployeeId &&
                x.LeaveTypeId == leave.LeaveTypeId &&
                x.Year == leave.StartDate.Year);
            if (balance is not null)
                balance.UsedDays = Math.Max(0, balance.UsedDays - leave.DaysRequested);
        }

        leave.Status = LeaveRequestStatus.Cancelled;
        leave.ReviewedBy = context.UserId;
        leave.ReviewedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = context.TenantId, UserId = context.UserId, UserName = User.Identity?.Name,
            Action = "CANCEL", EntityType = "LeaveRequest", EntityId = id, Details = "Leave request cancelled."
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }

    [HttpPatch("requests/{id:int}/status")]
    [Authorize(Policy = "CanApprove")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateLeaveStatusRequest request)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var leave = await db.LeaveRequests.SingleOrDefaultAsync(x =>
            x.Id == id && x.TenantId == context.TenantId);

        if (leave is null)
            return NotFound();

        if (leave.Status != LeaveRequestStatus.Pending)
            return Conflict(new { message = "Only pending leave requests can be reviewed." });

        if (request.Status is not (LeaveRequestStatus.Approved or LeaveRequestStatus.Rejected or LeaveRequestStatus.Cancelled))
            return BadRequest(new { message = "Invalid review status." });

        if (request.Status == LeaveRequestStatus.Approved)
        {
            var balance = await db.LeaveBalances.SingleOrDefaultAsync(x =>
                x.TenantId == context.TenantId &&
                x.EmployeeId == leave.EmployeeId &&
                x.LeaveTypeId == leave.LeaveTypeId &&
                x.Year == leave.StartDate.Year);

            if (balance is null)
                return Conflict(new { message = "No leave balance exists for this employee and leave type." });

            var available = balance.AllocatedDays - balance.UsedDays;
            if (available < leave.DaysRequested)
                return Conflict(new
                {
                    message = $"Insufficient leave balance. Available: {available:0.##} days; requested: {leave.DaysRequested:0.##} days."
                });

            balance.UsedDays += leave.DaysRequested;
        }

        leave.Status = request.Status;
        leave.ReviewedBy = context.UserId;
        leave.ReviewedAt = DateTime.UtcNow;

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = context.TenantId,
            UserId = context.UserId,
            UserName = User.Identity?.Name,
            Action = "UPDATE",
            EntityType = "LeaveRequest",
            EntityId = id,
            Details = $"Status changed to {request.Status}."
        });

        await db.SaveChangesAsync();
        await notifications.NotifyEmployeeAsync(context.TenantId, leave.EmployeeId, "Leave", $"Leave request {request.Status}", $"Your leave request has been {request.Status.ToString().ToLowerInvariant()}.", "LeaveRequest", leave.Id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return NoContent();
    }
    private async Task AuditAsync(string action, string entity, int id, string details)
    {
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = context.TenantId,
            UserId = context.UserId,
            UserName = User.Identity?.Name,
            Action = action,
            EntityType = entity,
            EntityId = id,
            Details = details,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await db.SaveChangesAsync();
    }

}
