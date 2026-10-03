using BluePrintHr.Api.Contracts;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Models;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController(BluePrintHrDbContext db, IRequestContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttendanceDto>>> List([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? employeeId)
    {
        var query = db.AttendanceRecords.AsNoTracking().Where(x => x.TenantId == context.TenantId);

        if (context.Role == nameof(UserRole.Employee))
        {
            if (!context.EmployeeId.HasValue)
                return Forbid();

            query = query.Where(x => x.EmployeeId == context.EmployeeId.Value);

            if (employeeId.HasValue && employeeId.Value != context.EmployeeId.Value)
                return Forbid();
        }
        else if (employeeId.HasValue)
        {
            query = query.Where(x => x.EmployeeId == employeeId.Value);
        }

        if (from.HasValue) query = query.Where(x => x.AttendanceDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.AttendanceDate <= to.Value.Date);

        var rows = await query.Include(x => x.Employee).OrderByDescending(x => x.AttendanceDate).ThenBy(x => x.EmployeeId).Take(500).ToListAsync();
        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpGet("summary")]
    public async Task<ActionResult<AttendanceSummaryDto>> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var start = (from ?? DateTime.UtcNow.Date.AddDays(-29)).Date;
        var end = (to ?? DateTime.UtcNow.Date).Date;
        var query = db.AttendanceRecords.AsNoTracking()
            .Where(x => x.TenantId == context.TenantId && x.AttendanceDate >= start && x.AttendanceDate <= end);

        if (context.Role == nameof(UserRole.Employee))
        {
            if (!context.EmployeeId.HasValue)
                return Forbid();

            query = query.Where(x => x.EmployeeId == context.EmployeeId.Value);
        }

        var rows = await query.ToListAsync();

        return Ok(new AttendanceSummaryDto(
            start, end, rows.Count(x => x.Status == AttendanceStatus.Present),
            rows.Count(x => x.Status == AttendanceStatus.Late),
            rows.Count(x => x.Status == AttendanceStatus.Absent),
            rows.Count(x => x.Status == AttendanceStatus.Leave),
            rows.Count(x => x.Status == AttendanceStatus.HalfDay),
            rows.Sum(x => x.HoursWorked)));
    }

    [HttpPost]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<AttendanceDto>> Create(CreateAttendanceRequest request)
    {
        if (request.AttendanceDate.Date > DateTime.UtcNow.Date) return BadRequest(new { message = "Attendance cannot be recorded for a future date." });
        var employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == request.EmployeeId && x.TenantId == context.TenantId);
        if (employee is null) return NotFound(new { message = "Employee not found." });

        var existing = await db.AttendanceRecords.SingleOrDefaultAsync(x => x.TenantId == context.TenantId && x.EmployeeId == request.EmployeeId && x.AttendanceDate == request.AttendanceDate.Date);
        if (existing is not null) return Conflict(new { message = "Attendance already exists for this employee and date." });

        var row = new AttendanceRecord
        {
            TenantId = context.TenantId, EmployeeId = employee.Id, AttendanceDate = request.AttendanceDate.Date,
            CheckIn = request.CheckIn, CheckOut = request.CheckOut, Status = request.Status,
            HoursWorked = request.HoursWorked ?? CalculateHours(request.CheckIn, request.CheckOut), Notes = request.Notes
        };
        db.AttendanceRecords.Add(row);
        await db.SaveChangesAsync();
        return Ok(ToDto(await db.AttendanceRecords.Include(x => x.Employee).SingleAsync(x => x.Id == row.Id)));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<AttendanceDto>> Update(int id, CreateAttendanceRequest request)
    {
        var row = await db.AttendanceRecords.Include(x => x.Employee).SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (row is null) return NotFound();
        row.CheckIn = request.CheckIn; row.CheckOut = request.CheckOut; row.Status = request.Status;
        row.HoursWorked = request.HoursWorked ?? CalculateHours(request.CheckIn, request.CheckOut); row.Notes = request.Notes;
        await db.SaveChangesAsync();
        return Ok(ToDto(row));
    }

    private static decimal CalculateHours(DateTime? start, DateTime? end) => start.HasValue && end.HasValue && end > start ? (decimal)(end.Value - start.Value).TotalHours : 0;

    private static AttendanceDto ToDto(AttendanceRecord x) => new(x.Id, x.EmployeeId, $"{x.Employee.FirstName} {x.Employee.LastName}", x.Employee.EmployeeNo, x.AttendanceDate, x.CheckIn, x.CheckOut, x.Status.ToString(), x.HoursWorked, x.Notes);
}
