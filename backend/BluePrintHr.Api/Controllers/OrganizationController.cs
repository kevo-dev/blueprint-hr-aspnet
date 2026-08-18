using BluePrintHr.Api.Contracts;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Models;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/organization")]
[Authorize]
public class OrganizationController(BluePrintHrDbContext db, IRequestContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OrganizationDto>> Get()
    {
        var tenant = context.TenantId;
        return Ok(new OrganizationDto(
            await db.Branches.AsNoTracking().Where(x => x.TenantId == tenant).OrderBy(x => x.Name).ToListAsync(),
            await db.Departments.AsNoTracking().Where(x => x.TenantId == tenant).OrderBy(x => x.Name).ToListAsync(),
            await db.Designations.AsNoTracking().Where(x => x.TenantId == tenant).OrderBy(x => x.Name).ToListAsync(),
            await db.Grades.AsNoTracking().Where(x => x.TenantId == tenant).OrderBy(x => x.Name).ToListAsync(),
            await db.EmploymentTypes.AsNoTracking().Where(x => x.TenantId == tenant).OrderBy(x => x.Name).ToListAsync()));
    }

    [HttpPost("branches")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<Branch>> CreateBranch(CreateBranchRequest request)
    {
        if (await db.Branches.AnyAsync(x => x.TenantId == context.TenantId && x.Name == request.Name)) return Conflict(new { message = "Branch already exists." });
        var branch = new Branch { TenantId = context.TenantId, Name = request.Name.Trim(), Code = request.Code, Location = request.Location };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        await WriteAudit("CREATE", "Branch", branch.Id, branch.Name);
        return Created($"/api/organization/branches/{branch.Id}", branch);
    }

    [HttpPost("departments")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<Department>> CreateDepartment(CreateDepartmentRequest request)
    {
        if (request.BranchId.HasValue && !await db.Branches.AnyAsync(x => x.Id == request.BranchId && x.TenantId == context.TenantId)) return BadRequest(new { message = "Branch is outside the current tenant." });
        if (await db.Departments.AnyAsync(x => x.TenantId == context.TenantId && x.Name == request.Name)) return Conflict(new { message = "Department already exists." });
        var department = new Department { TenantId = context.TenantId, Name = request.Name.Trim(), Code = request.Code, BranchId = request.BranchId };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        await WriteAudit("CREATE", "Department", department.Id, department.Name);
        return Created($"/api/organization/departments/{department.Id}", department);
    }

    private async Task WriteAudit(string action, string entityType, int entityId, string details)
    {
        db.AuditLogs.Add(new AuditLog { TenantId = context.TenantId, UserId = context.UserId, UserName = User.Identity?.Name, Action = action, EntityType = entityType, EntityId = entityId, Details = details });
        await db.SaveChangesAsync();
    }
}
