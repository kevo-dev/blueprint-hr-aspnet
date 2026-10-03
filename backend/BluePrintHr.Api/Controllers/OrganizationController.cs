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
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Branch name is required." });
        if (await db.Branches.AnyAsync(x => x.TenantId == context.TenantId && x.Name == name)) return Conflict(new { message = "Branch already exists." });
        var branch = new Branch { TenantId = context.TenantId, Name = name, Code = request.Code?.Trim(), Location = request.Location?.Trim() };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        await WriteAudit("CREATE", "Branch", branch.Id, branch.Name);
        return Created($"/api/organization/branches/{branch.Id}", branch);
    }

    [HttpPut("branches/{id:int}")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<Branch>> UpdateBranch(int id, UpdateBranchRequest request)
    {
        var branch = await db.Branches.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (branch is null) return NotFound();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Branch name is required." });
        if (await db.Branches.AnyAsync(x => x.TenantId == context.TenantId && x.Name == name && x.Id != id)) return Conflict(new { message = "Branch already exists." });
        branch.Name = name; branch.Code = request.Code?.Trim(); branch.Location = request.Location?.Trim();
        await db.SaveChangesAsync();
        await WriteAudit("UPDATE", "Branch", id, name);
        return Ok(branch);
    }

    [HttpDelete("branches/{id:int}")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<IActionResult> DeleteBranch(int id)
    {
        var branch = await db.Branches.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (branch is null) return NotFound();
        if (await db.Departments.AnyAsync(x => x.TenantId == context.TenantId && x.BranchId == id) ||
            await db.Employees.AnyAsync(x => x.TenantId == context.TenantId && x.BranchId == id))
            return Conflict(new { message = "Branch is in use. Reassign departments and employees before deleting it." });
        db.Branches.Remove(branch);
        await db.SaveChangesAsync();
        await WriteAudit("DELETE", "Branch", id, branch.Name);
        return NoContent();
    }

    [HttpPost("departments")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<Department>> CreateDepartment(CreateDepartmentRequest request)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Department name is required." });
        if (request.BranchId.HasValue && !await db.Branches.AnyAsync(x => x.Id == request.BranchId && x.TenantId == context.TenantId)) return BadRequest(new { message = "Branch is outside the current tenant." });
        if (await db.Departments.AnyAsync(x => x.TenantId == context.TenantId && x.Name == name)) return Conflict(new { message = "Department already exists." });
        var department = new Department { TenantId = context.TenantId, Name = name, Code = request.Code?.Trim(), BranchId = request.BranchId };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        await WriteAudit("CREATE", "Department", department.Id, department.Name);
        return Created($"/api/organization/departments/{department.Id}", department);
    }

    [HttpPut("departments/{id:int}")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<Department>> UpdateDepartment(int id, UpdateDepartmentRequest request)
    {
        var department = await db.Departments.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (department is null) return NotFound();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Department name is required." });
        if (request.BranchId.HasValue && !await db.Branches.AnyAsync(x => x.Id == request.BranchId && x.TenantId == context.TenantId)) return BadRequest(new { message = "Branch is outside the current tenant." });
        if (await db.Departments.AnyAsync(x => x.TenantId == context.TenantId && x.Name == name && x.Id != id)) return Conflict(new { message = "Department already exists." });
        department.Name = name; department.Code = request.Code?.Trim(); department.BranchId = request.BranchId;
        await db.SaveChangesAsync();
        await WriteAudit("UPDATE", "Department", id, name);
        return Ok(department);
    }

    [HttpDelete("departments/{id:int}")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<IActionResult> DeleteDepartment(int id)
    {
        var department = await db.Departments.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (department is null) return NotFound();
        if (await db.Employees.AnyAsync(x => x.TenantId == context.TenantId && x.DepartmentId == id))
            return Conflict(new { message = "Department is in use. Reassign employees before deleting it." });
        db.Departments.Remove(department);
        await db.SaveChangesAsync();
        await WriteAudit("DELETE", "Department", id, department.Name);
        return NoContent();
    }

    [HttpPost("designations")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<Designation>> CreateDesignation(CreateDesignationRequest request)
        => await SaveDesignation(request.Name, null);

    [HttpPut("designations/{id:int}")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<Designation>> UpdateDesignation(int id, UpdateDesignationRequest request)
        => await SaveDesignation(request.Name, id);

    private async Task<ActionResult<Designation>> SaveDesignation(string rawName, int? id)
    {
        var name = rawName.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Designation name is required." });
        var existing = await db.Designations.SingleOrDefaultAsync(x => x.TenantId == context.TenantId && x.Name == name && (!id.HasValue || x.Id != id.Value));
        if (existing is not null) return Conflict(new { message = "Designation already exists." });
        Designation designation;
        if (id.HasValue)
        {
            designation = await db.Designations.SingleOrDefaultAsync(x => x.Id == id.Value && x.TenantId == context.TenantId) ?? null!;
            if (designation is null) return NotFound();
            designation.Name = name;
        }
        else
        {
            designation = new Designation { TenantId = context.TenantId, Name = name };
            db.Designations.Add(designation);
        }
        await db.SaveChangesAsync();
        await WriteAudit(id.HasValue ? "UPDATE" : "CREATE", "Designation", designation.Id, designation.Name);
        return Ok(designation);
    }

    [HttpDelete("designations/{id:int}")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<IActionResult> DeleteDesignation(int id)
    {
        var item = await db.Designations.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (item is null) return NotFound();
        if (await db.Employees.AnyAsync(x => x.TenantId == context.TenantId && x.DesignationId == id))
            return Conflict(new { message = "Designation is in use. Reassign employees before deleting it." });
        db.Designations.Remove(item); await db.SaveChangesAsync(); await WriteAudit("DELETE", "Designation", id, item.Name); return NoContent();
    }

    [HttpPost("grades")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<Grade>> CreateGrade(CreateGradeRequest request)
        => await SaveGrade(request.Name, request.Level, request.MinSalary, request.MaxSalary, null);

    [HttpPut("grades/{id:int}")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<Grade>> UpdateGrade(int id, UpdateGradeRequest request)
        => await SaveGrade(request.Name, request.Level, request.MinSalary, request.MaxSalary, id);

    private async Task<ActionResult<Grade>> SaveGrade(string rawName, string? level, decimal minSalary, decimal maxSalary, int? id)
    {
        var name = rawName.Trim();
        if (string.IsNullOrWhiteSpace(name) || minSalary < 0 || maxSalary < minSalary) return BadRequest(new { message = "Grade name and salary range are invalid." });
        if (await db.Grades.AnyAsync(x => x.TenantId == context.TenantId && x.Name == name && (!id.HasValue || x.Id != id.Value))) return Conflict(new { message = "Grade already exists." });
        Grade item;
        if (id.HasValue) { item = await db.Grades.SingleOrDefaultAsync(x => x.Id == id.Value && x.TenantId == context.TenantId) ?? null!; if (item is null) return NotFound(); }
        else { item = new Grade { TenantId = context.TenantId }; db.Grades.Add(item); }
        item.Name = name; item.Level = level?.Trim(); item.MinSalary = minSalary; item.MaxSalary = maxSalary;
        await db.SaveChangesAsync(); await WriteAudit(id.HasValue ? "UPDATE" : "CREATE", "Grade", item.Id, item.Name); return Ok(item);
    }

    [HttpDelete("grades/{id:int}")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<IActionResult> DeleteGrade(int id)
    {
        var item = await db.Grades.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (item is null) return NotFound();
        if (await db.Employees.AnyAsync(x => x.TenantId == context.TenantId && x.GradeId == id))
            return Conflict(new { message = "Grade is in use. Reassign employees before deleting it." });
        db.Grades.Remove(item); await db.SaveChangesAsync(); await WriteAudit("DELETE", "Grade", id, item.Name); return NoContent();
    }

    [HttpPost("employment-types")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<EmploymentType>> CreateEmploymentType(CreateEmploymentTypeRequest request)
        => await SaveEmploymentType(request.Name, request.Description, null);

    [HttpPut("employment-types/{id:int}")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<ActionResult<EmploymentType>> UpdateEmploymentType(int id, UpdateEmploymentTypeRequest request)
        => await SaveEmploymentType(request.Name, request.Description, id);

    private async Task<ActionResult<EmploymentType>> SaveEmploymentType(string rawName, string? description, int? id)
    {
        var name = rawName.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Employment type name is required." });
        if (await db.EmploymentTypes.AnyAsync(x => x.TenantId == context.TenantId && x.Name == name && (!id.HasValue || x.Id != id.Value))) return Conflict(new { message = "Employment type already exists." });
        EmploymentType item;
        if (id.HasValue) { item = await db.EmploymentTypes.SingleOrDefaultAsync(x => x.Id == id.Value && x.TenantId == context.TenantId) ?? null!; if (item is null) return NotFound(); }
        else { item = new EmploymentType { TenantId = context.TenantId }; db.EmploymentTypes.Add(item); }
        item.Name = name; item.Description = description?.Trim();
        await db.SaveChangesAsync(); await WriteAudit(id.HasValue ? "UPDATE" : "CREATE", "EmploymentType", item.Id, item.Name); return Ok(item);
    }

    [HttpDelete("employment-types/{id:int}")]
    [Authorize(Policy = "CanManageEmployees")]
    public async Task<IActionResult> DeleteEmploymentType(int id)
    {
        var item = await db.EmploymentTypes.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId);
        if (item is null) return NotFound();
        if (await db.Employees.AnyAsync(x => x.TenantId == context.TenantId && x.EmploymentTypeId == id))
            return Conflict(new { message = "Employment type is in use. Reassign employees before deleting it." });
        db.EmploymentTypes.Remove(item); await db.SaveChangesAsync(); await WriteAudit("DELETE", "EmploymentType", id, item.Name); return NoContent();
    }

    private async Task WriteAudit(string action, string entityType, int entityId, string details)
    {
        db.AuditLogs.Add(new AuditLog { TenantId = context.TenantId, UserId = context.UserId, UserName = User.Identity?.Name, Action = action, EntityType = entityType, EntityId = entityId, Details = details });
        await db.SaveChangesAsync();
    }
}
