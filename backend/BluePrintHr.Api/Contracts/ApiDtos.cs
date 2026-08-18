using BluePrintHr.Api.Models;

namespace BluePrintHr.Api.Contracts;

public record LoginRequest(string Email, string Password);
public record UserDto(int Id, string Name, string Email, string Role, int TenantId, int? EmployeeId);
public record TenantDto(int Id, string CompanyName, string? KraPin, string? Email, string? Phone, string? Address, string Status);
public record DashboardDto(int TotalEmployees, decimal MonthlyGross, int Branches, int Departments, string PayrollStatus, TenantDto Tenant, UserDto User);

public record EmployeeDto(
    int Id,
    string EmployeeNo,
    string? PayrollNo,
    string FullName,
    string? Email,
    string? Phone,
    string KraPin,
    string? NssfNo,
    string? ShifNo,
    string EmploymentStatus,
    decimal BasicSalary,
    string? BankName,
    string? AccountNumber,
    int? DepartmentId,
    int? BranchId);

public record CreateEmployeeRequest(
    string EmployeeNo,
    string FirstName,
    string LastName,
    string KraPin,
    decimal BasicSalary,
    string? MiddleName,
    string? PayrollNo,
    string? Email,
    string? Phone,
    string? NssfNo,
    string? ShifNo,
    int? BranchId,
    int? DepartmentId,
    string? BankName,
    string? BankBranch,
    string? AccountNumber);

public record CreateBranchRequest(string Name, string? Code, string? Location);
public record CreateDepartmentRequest(string Name, string? Code, int? BranchId);
public record OrganizationDto(IReadOnlyList<Branch> Branches, IReadOnlyList<Department> Departments, IReadOnlyList<Designation> Designations, IReadOnlyList<Grade> Grades, IReadOnlyList<EmploymentType> EmploymentTypes);

public record PayrollPeriodDto(int Id, string Name, int Month, int Year, string Status, DateTime? ProcessedAt);
public record PayrollTransactionDto(int Id, int EmployeeId, string EmployeeName, decimal GrossPay, decimal Paye, decimal Nssf, decimal Shif, decimal HousingLevy, decimal TotalDeductions, decimal NetPay, string Status);
public record PayrollProcessRequest(int PayrollPeriodId, decimal Allowances = 0, decimal OtherDeductions = 0);

public record LeaveTypeDto(int Id, string Name, int DefaultDays, bool Paid, string? Description);
public record LeaveBalanceDto(int Id, int EmployeeId, int LeaveTypeId, string LeaveType, int Year, decimal AllocatedDays, decimal UsedDays, decimal AvailableDays);
public record LeaveRequestDto(int Id, int EmployeeId, string EmployeeName, int LeaveTypeId, string LeaveType, DateTime StartDate, DateTime EndDate, decimal DaysRequested, string? Reason, string Status, DateTime CreatedAt);
public record CreateLeaveRequest(int EmployeeId, int LeaveTypeId, DateTime StartDate, DateTime EndDate, decimal DaysRequested, string? Reason);
public record UpdateLeaveStatusRequest(LeaveRequestStatus Status);

public record AuditLogDto(int Id, string Action, string EntityType, int? EntityId, string? UserName, string? Details, DateTime CreatedAt);
public record ReportDefinitionDto(int Id, string Name, string Description, string ReportPath, string? LaunchUrl);
