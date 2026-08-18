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
public class LeaveController(BluePrintHrDbContext db, IRequestContext context) : ControllerBase
{
    [HttpGet("types")]
    public async Task<ActionResult<IReadOnlyList<LeaveTypeDto>>> Types()
    {
        var types = await db.LeaveTypes.AsNoTracking().Where(x => x.TenantId == context.TenantId).OrderBy(x => x.Name).ToListAsync();
        return Ok(types.Select(x => new LeaveTypeDto(x.Id, x.Name, x.DefaultDays, x.Paid, x.Description)).ToList());
    }

    [HttpGet("balances")]
    public async Task<ActionResult<IReadOnlyList<LeaveBalanceDto>>> Balances([FromQuery] int? employeeId = null)
    {
        var effectiveEmployeeId = context.CanManageEmployees ? employeeId : context.EmployeeId;
        var query = db.LeaveBalances.AsNoTracking().Where(x => x.TenantId == context.TenantId);
        if (effectiveEmployeeId.HasValue) query = query.Where(x => x.EmployeeId == effectiveEmployeeId.Value);
        var balances = await query.Join(db.LeaveTypes, balance => balance.LeaveTypeId, type => type.Id, (balance, type) => new LeaveBalanceDto(balance.Id, balance.EmployeeId, balance.LeaveTypeId, type.Name, balance.Year, balance.AllocatedDays, balance.UsedDays, Math.Max(balance.AllocatedDays - balance.UsedDays, 0))).ToListAsync();
        return Ok(balances);
    }

    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> Requests([FromQuery] int? employeeId = null)
    {
        var effectiveEmployeeId = context.CanManageEmployees ? employeeId : context.EmployeeId;
        var query = db.LeaveRequests.AsNoTracking().Where(x => x.TenantId == context.TenantId);
        if (effectiveEmployeeId.HasValue) query = query.Where(x => x.EmployeeId == effectiveEmployeeId.Value);
        var requests = await query.Join(db.Employees, request => request.EmployeeId, employee => employee.Id, (request, employee) => new { request, employee })
            .Join(db.LeaveTypes, item => item.request.LeaveTypeId, type => type.Id, (item, type) => new LeaveRequestDto(item.request.Id, item.request.EmployeeId, $"{item.employee.FirstName} {item.employee.LastName}", item.request.LeaveTypeId, type.Name, item.request.StartDate, item.request.EndDate, item.request.DaysRequested, item.request.Reason, item.request.Status.ToString(), item.request.CreatedAt))
            .OrderByDescending(x => x.CreatedAt).ToListAsync();
        return Ok(requests);
    }

    [HttpPost("requests")]
    public async Task<ActionResult<LeaveRequestDto>> CreateRequest(CreateLeaveRequest request)
    {
        var employeeId = context.CanManageEmployees ? request.EmployeeId : context.EmployeeId;
        if (!employeeId.HasValue) return BadRequest(new { message = "No employee profile is linked to this user." });
        var employeeExists = await db.Employees.AnyAsync(x => x.Id == employeeId.Value && x.TenantId == context.TenantId);
        var leaveType = await db.LeaveTypes.SingleOrDefaultAsync(x => x.Id == request.LeaveTypeId && x.TenantId == context.TenantId);
        if (!employeeExists || leaveType is null) return BadRequest(new { message = "Employee or leave type is outside the current tenant." });
        if (request.EndDate.Date < request.StartDate.Date || request.DaysRequested <= 0) return BadRequest(new { message = "Leave dates and requested days are invalid." });

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
        db.AuditLogs.Add(new AuditLog { TenantId = context.TenantId, UserId = context.UserId, UserName = User.Identity?.Name, Action = "CREATE", EntityType = "LeaveRequest", Details = $"Submitted {request.DaysRequested} days." });
        await db.SaveChangesAsync();
        return Ok(new LeaveRequestDto(leave.Id, leave.EmployeeId, "", leave.LeaveTypeId, leaveType.Name, leave.StartDate, leave.EndDate, leave.DaysRequested, leave.Reason, leave.Status.ToString(), leave.CreatedAt));
    }

    [HttpPatch("requests/{id:int}/status")]
    [Authorize(Policy = "CanApprove")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateLeaveStatusRequest request)
    {
        var leave = await db.LeaveRequests.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (leave is null) return NotFound();
        if (leave.Status == LeaveRequestStatus.Approved && request.Status != LeaveRequestStatus.Approved) return Conflict(new { message = "Approved leave cannot be reversed in this workflow." });
        leave.Status = request.Status;
        leave.ReviewedBy = context.UserId;
        leave.ReviewedAt = DateTime.UtcNow;
        if (request.Status == LeaveRequestStatus.Approved)
        {
            var balance = await db.LeaveBalances.SingleOrDefaultAsync(x => x.TenantId == context.TenantId && x.EmployeeId == leave.EmployeeId && x.LeaveTypeId == leave.LeaveTypeId && x.Year == leave.StartDate.Year);
            if (balance is not null) balance.UsedDays += leave.DaysRequested;
        }
        db.AuditLogs.Add(new AuditLog { TenantId = context.TenantId, UserId = context.UserId, UserName = User.Identity?.Name, Action = "UPDATE", EntityType = "LeaveRequest", EntityId = id, Details = $"Status changed to {request.Status}." });
        await db.SaveChangesAsync();
        return NoContent();
    }
}
