using BluePrintHr.Api.Contracts;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = "CanViewAudit")]
public class AuditController(BluePrintHrDbContext db, IRequestContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> List()
    {
        var rows = await db.AuditLogs.AsNoTracking().Where(x => x.TenantId == context.TenantId).OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync();
        return Ok(rows.Select(x => new AuditLogDto(x.Id, x.Action, x.EntityType, x.EntityId, x.UserName, x.Details, x.CreatedAt)).ToList());
    }
}

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController(BluePrintHrDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportDefinitionDto>>> List()
    {
        var baseUrl = configuration["Ssrs:ReportServerUrl"]?.TrimEnd('/');
        var reports = await db.ReportDefinitions.AsNoTracking().Where(x => x.Enabled).OrderBy(x => x.Name).ToListAsync();
        return Ok(reports.Select(x => new ReportDefinitionDto(x.Id, x.Name, x.Description, x.ReportPath, baseUrl is null ? null : $"{baseUrl}{x.ReportPath}")).ToList());
    }
}
