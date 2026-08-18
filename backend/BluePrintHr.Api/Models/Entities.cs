namespace BluePrintHr.Api.Models;

public enum UserRole
{
    SuperAdmin,
    CompanyAdmin,
    HrManager,
    PayrollManager,
    Employee
}

public enum TenantStatus
{
    Active,
    Suspended,
    Trial
}

public enum PayrollStatus
{
    Open,
    Processing,
    Approved,
    Locked
}

public enum LeaveRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

public class User
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Employee;
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSignedIn { get; set; }
}

public class Tenant
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? KraPin { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Subdomain { get; set; }
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

public class Branch
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Location { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Department
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Designation
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class Grade
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Level { get; set; }
    public decimal MinSalary { get; set; }
    public decimal MaxSalary { get; set; }
}

public class EmploymentType
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class Employee
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string EmployeeNo { get; set; } = string.Empty;
    public string? PayrollNo { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? IdNo { get; set; }
    public string KraPin { get; set; } = string.Empty;
    public string? NssfNo { get; set; }
    public string? ShifNo { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int? BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int? DesignationId { get; set; }
    public int? GradeId { get; set; }
    public int? EmploymentTypeId { get; set; }
    public DateTime? EmploymentDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string EmploymentStatus { get; set; } = "Active";
    public decimal BasicSalary { get; set; }
    public string? BankName { get; set; }
    public string? BankBranch { get; set; }
    public string? AccountNumber { get; set; }
}

public class PayrollPeriod
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public PayrollStatus Status { get; set; } = PayrollStatus.Open;
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<PayrollTransaction> Transactions { get; set; } = new List<PayrollTransaction>();
}

public class PayrollTransaction
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int PayrollPeriodId { get; set; }
    public PayrollPeriod PayrollPeriod { get; set; } = null!;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public decimal BasicSalary { get; set; }
    public decimal Allowances { get; set; }
    public decimal GrossPay { get; set; }
    public decimal TaxablePay { get; set; }
    public decimal Paye { get; set; }
    public decimal PersonalRelief { get; set; }
    public decimal Nssf { get; set; }
    public decimal Shif { get; set; }
    public decimal HousingLevy { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class LeaveType
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DefaultDays { get; set; }
    public bool Paid { get; set; } = true;
    public string? Description { get; set; }
}

public class LeaveBalance
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int EmployeeId { get; set; }
    public int LeaveTypeId { get; set; }
    public int Year { get; set; }
    public decimal AllocatedDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal AvailableDays => Math.Max(AllocatedDays - UsedDays, 0);
}

public class LeaveRequest
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int EmployeeId { get; set; }
    public int LeaveTypeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal DaysRequested { get; set; }
    public string? Reason { get; set; }
    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;
    public int? ReviewedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ReportDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ReportPath { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
