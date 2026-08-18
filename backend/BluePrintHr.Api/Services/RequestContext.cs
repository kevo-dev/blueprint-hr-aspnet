using System.Security.Claims;

namespace BluePrintHr.Api.Services;

public interface IRequestContext
{
    bool IsAuthenticated { get; }
    int UserId { get; }
    int TenantId { get; }
    int? EmployeeId { get; }
    string Role { get; }
    bool CanManageEmployees { get; }
    bool CanManagePayroll { get; }
    bool CanApprove { get; }
}

public sealed class RequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
    public int UserId => ParseInt(ClaimTypes.NameIdentifier) ?? 0;
    public int TenantId => ParseInt("tenant_id") ?? 0;
    public int? EmployeeId => ParseInt("employee_id");
    public string Role => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    public bool CanManageEmployees => Role is "SuperAdmin" or "CompanyAdmin" or "HrManager";
    public bool CanManagePayroll => Role is "SuperAdmin" or "CompanyAdmin" or "PayrollManager";
    public bool CanApprove => Role is "SuperAdmin" or "CompanyAdmin" or "HrManager" or "PayrollManager";

    private int? ParseInt(string claimType)
    {
        var value = User.FindFirstValue(claimType);
        return int.TryParse(value, out var result) ? result : null;
    }
}
