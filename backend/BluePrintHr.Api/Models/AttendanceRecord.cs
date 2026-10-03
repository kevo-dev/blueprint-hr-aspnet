namespace BluePrintHr.Api.Models;

public class AttendanceRecord
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateTime AttendanceDate { get; set; }
    public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public decimal HoursWorked { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum AttendanceStatus { Present, Late, Absent, Leave, Holiday, Weekend, HalfDay }
