using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/employees/import")]
[Authorize(Policy = "CanManageEmployees")]
public class EmployeeImportController(BluePrintHrDbContext db, IRequestContext context) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<EmployeeImportResult>> Import(IFormFile file, [FromQuery] bool updateExisting = true, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Please select a CSV, JSON or XLSX staff file." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        List<Dictionary<string, string?>> rows;
        await using var stream = file.OpenReadStream();

        if (extension == ".csv")
            rows = ParseCsv(await new StreamReader(stream, Encoding.UTF8, true).ReadToEndAsync(cancellationToken));
        else if (extension == ".json")
            rows = await ParseJsonAsync(stream, cancellationToken);
        else if (extension == ".xlsx")
            rows = ParseXlsx(stream);
        else if (extension == ".xls")
            return BadRequest(new { message = "Legacy .xls files are not supported. Save the workbook as .xlsx and import again." });
        else
            return BadRequest(new { message = "Supported formats are CSV, JSON and XLSX." });

        var result = new EmployeeImportResult();
        var existing = await db.Employees.Where(x => x.TenantId == context.TenantId).ToDictionaryAsync(x => x.EmployeeNo, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var raw in rows)
        {
            result.Total++;
            try
            {
                var row = Normalize(raw);
                var employeeNo = Get(row, "employeeno", "employee_no", "employeeid", "employee_id", "staffno", "staff_no", "payrollno", "payroll_no");
                var first = Get(row, "firstname", "first_name", "givenname", "given_name");
                var last = Get(row, "lastname", "last_name", "surname", "familyname", "family_name");
                var fullName = Get(row, "name", "fullname", "full_name", "employee_name", "staffname", "staff_name");

                if (string.IsNullOrWhiteSpace(employeeNo) && !string.IsNullOrWhiteSpace(fullName))
                {
                    var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    first ??= parts.FirstOrDefault();
                    last ??= parts.Length > 1 ? parts[^1] : null;
                    if (parts.Length > 2) row["middlename"] = string.Join(' ', parts.Skip(1).Take(parts.Length - 2));
                    employeeNo = Get(row, "idno", "id_no", "nationalid", "national_id", "id");
                }

                if (string.IsNullOrWhiteSpace(employeeNo) || string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last))
                    throw new InvalidOperationException("Employee number/identifier, first name and last name are required.");

                var kraPin = Get(row, "krapin", "kra_pin", "pin", "taxpin", "tax_pin") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(kraPin)) throw new InvalidOperationException("KRA PIN is required.");

                var employee = existing.TryGetValue(employeeNo, out var found) && updateExisting ? found : new Employee { TenantId = context.TenantId, EmployeeNo = employeeNo };
                var isNew = employee.Id == 0;

                employee.EmployeeNo = employeeNo;
                employee.PayrollNo = Get(row, "payrollno", "payroll_no");
                employee.FirstName = first;
                employee.MiddleName = Get(row, "middlename", "middle_name", "othernames", "other_names");
                employee.LastName = last;
                employee.Gender = Get(row, "gender", "sex");
                employee.IdNo = Get(row, "idno", "id_no", "nationalid", "national_id", "nationalidentity");
                employee.KraPin = kraPin.ToUpperInvariant();
                employee.NssfNo = Get(row, "nssf", "nssfno", "nssf_no", "nssfnumber", "nssf_number");
                employee.ShifNo = Get(row, "sha", "shif", "shifno", "shif_no", "sha_no", "sha_number", "nhif", "nhifno", "nhif_no");
                employee.Phone = Get(row, "phone", "mobile", "mobilephone", "mobile_phone", "telephone");
                employee.Email = Get(row, "email", "emailaddress", "email_address");
                employee.BasicSalary = DecimalValue(Get(row, "basicsalary", "basic_salary", "salary", "basic"));
                employee.BankName = Get(row, "bank", "bankname", "bank_name");
                employee.BankBranch = Get(row, "bankbranch", "bank_branch", "branch");
                employee.AccountNumber = Get(row, "account", "accountnumber", "account_number", "bankaccount", "bank_account");
                employee.EmploymentStatus = Get(row, "employmentstatus", "employment_status", "status") ?? "Active";
                employee.EmploymentDate = DateValue(Get(row, "employmentdate", "employment_date", "datejoined", "date_joined", "startdate", "start_date"));
                employee.TerminationDate = DateValue(Get(row, "terminationdate", "termination_date", "enddate", "end_date"));
                if (isNew) db.Employees.Add(employee);
                await db.SaveChangesAsync(cancellationToken);
                existing[employee.EmployeeNo] = employee;
                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Errors.Add(new EmployeeImportError(result.Total, ex.Message));
            }
        }

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = context.TenantId, UserId = context.UserId, UserName = User.Identity?.Name,
            Action = "IMPORT", EntityType = "Employee", Details = $"Imported {result.Imported} of {result.Total} rows."
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(result);
    }

    private static Dictionary<string, string?> Normalize(Dictionary<string, string?> row) =>
        row.ToDictionary(x => new string(x.Key.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant(), x => x.Value?.Trim());

    private static string? Get(Dictionary<string, string?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            var normalized = new string(key.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            if (row.TryGetValue(normalized, out var value) && !string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return null;
    }

    private static decimal DecimalValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)) return result;
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.GetCultureInfo("en-KE"), out result)) return result;
        throw new InvalidOperationException($"Invalid decimal value '{value}'.");
    }

    private static DateTime? DateValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)) return result.ToUniversalTime();
        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("en-KE"), DateTimeStyles.AssumeLocal, out result)) return result.ToUniversalTime();
        throw new InvalidOperationException($"Invalid date value '{value}'.");
    }

    private static List<Dictionary<string, string?>> ParseXlsx(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet is null) throw new InvalidOperationException("The workbook contains no worksheets.");

        var range = worksheet.RangeUsed();
        if (range is null) return new();

        var headers = range.FirstRow().Cells().Select(x => x.GetString().Trim()).ToList();
        var rows = new List<Dictionary<string, string?>>();
        foreach (var row in range.RowsUsed().Skip(1))
        {
            var item = new Dictionary<string, string?>();
            for (var i = 0; i < headers.Count; i++)
                item[headers[i]] = row.Cell(i + 1).GetString().Trim();
            rows.Add(item);
        }
        return rows;
    }

    private static async Task<List<Dictionary<string, string?>>> ParseJsonAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("JSON must contain an array of staff objects.");
        return doc.RootElement.EnumerateArray().Select(x => x.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.ToString())).ToList();
    }

    private static List<Dictionary<string, string?>> ParseCsv(string csv)
    {
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return new();
        var headers = SplitCsvLine(lines[0]);
        var rows = new List<Dictionary<string, string?>>();
        foreach (var line in lines.Skip(1))
        {
            var values = SplitCsvLine(line);
            var row = new Dictionary<string, string?>();
            for (var i = 0; i < headers.Count; i++) row[headers[i]] = i < values.Count ? values[i] : null;
            rows.Add(row);
        }
        return rows;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"' && (i == 0 || line[i - 1] != '\\')) quoted = !quoted;
            else if (ch == ',' && !quoted) { result.Add(current.ToString().Trim()); current.Clear(); }
            else current.Append(ch);
        }
        result.Add(current.ToString().Trim().Trim('"'));
        return result;
    }
}

public sealed class EmployeeImportResult
{
    public int Total { get; set; }
    public int Imported { get; set; }
    public List<EmployeeImportError> Errors { get; } = new();
}

public sealed record EmployeeImportError(int Row, string Error);
