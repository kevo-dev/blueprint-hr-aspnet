using BluePrintHr.Api.Contracts;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(BluePrintHrDbContext db, IRequestContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get()
    {
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(x => x.Id == context.TenantId);
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == context.UserId);
        var employees = db.Employees.Where(x => x.TenantId == context.TenantId && x.EmploymentStatus == "Active");
        var period = await db.PayrollPeriods.AsNoTracking().Where(x => x.TenantId == context.TenantId).OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).FirstOrDefaultAsync();
        var response = new DashboardDto(
            await employees.CountAsync(),
            await employees.SumAsync(x => (decimal?)x.BasicSalary) ?? 0,
            await db.Branches.CountAsync(x => x.TenantId == context.TenantId),
            await db.Departments.CountAsync(x => x.TenantId == context.TenantId),
            period?.Status.ToString() ?? "Open",
            new TenantDto(tenant.Id, tenant.CompanyName, tenant.KraPin, tenant.Email, tenant.Phone, tenant.Address, tenant.Status.ToString()),
            new UserDto(user.Id, user.Name, user.Email, AuthController.Label(user.Role), user.TenantId, user.EmployeeId));
        return Ok(response);
    }
}
