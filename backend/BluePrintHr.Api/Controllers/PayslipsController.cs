using System.Text;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BluePrintHr.Api.Controllers;

[ApiController]
[Route("api/payslips")]
[Authorize]
public class PayslipsController(BluePrintHrDbContext db, IRequestContext context) : ControllerBase
{
    [HttpGet("{transactionId:int}/pdf")]
    public async Task<IActionResult> Pdf(int transactionId)
    {
        var transaction = await db.PayrollTransactions
            .AsNoTracking()
            .Include(x => x.Employee)
            .Include(x => x.PayrollPeriod)
            .SingleOrDefaultAsync(x => x.Id == transactionId && x.TenantId == context.TenantId);

        if (transaction is null) return NotFound();
        if (!context.CanManagePayroll && transaction.EmployeeId != context.EmployeeId) return Forbid();

        var e = transaction.Employee;
        var lines = new[]
        {
            "BLUEPRINT HR",
            "EMPLOYEE PAYSLIP",
            "",
            $"Employee: {e.FirstName} {e.LastName}",
            $"Employee No: {e.EmployeeNo}",
            $"Payroll No: {e.PayrollNo ?? "-"}",
            $"Period: {transaction.PayrollPeriod.Name}",
            $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
            "",
            "EARNINGS",
            $"Basic salary: KES {transaction.BasicSalary:N2}",
            $"Allowances: KES {transaction.Allowances:N2}",
            $"Gross pay: KES {transaction.GrossPay:N2}",
            "",
            "DEDUCTIONS",
            $"PAYE: KES {transaction.Paye:N2}",
            $"NSSF: KES {transaction.Nssf:N2}",
            $"SHIF: KES {transaction.Shif:N2}",
            $"Housing levy: KES {transaction.HousingLevy:N2}",
            $"Other deductions: KES {transaction.OtherDeductions:N2}",
            $"Total deductions: KES {transaction.TotalDeductions:N2}",
            "",
            $"NET PAY: KES {transaction.NetPay:N2}",
            "",
            "This is a system-generated BluePrint HR payslip."
        };

        var bytes = SimplePdf.Create(lines);
        return File(bytes, "application/pdf", $"payslip-{e.EmployeeNo}-{transaction.PayrollPeriod.Year}-{transaction.PayrollPeriod.Month:00}.pdf");
    }

    private static class SimplePdf
    {
        public static byte[] Create(IReadOnlyList<string> lines)
        {
            var content = new StringBuilder();
            content.AppendLine("BT");
            content.AppendLine("/F1 11 Tf");
            content.AppendLine("50 760 Td");
            foreach (var line in lines)
            {
                content.Append('(').Append(Escape(line)).AppendLine(") Tj");
                content.AppendLine("0 -18 Td");
            }
            content.AppendLine("ET");

            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
                $"<< /Length {Encoding.ASCII.GetByteCount(content.ToString())} >>
stream
{content}endstream",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
            };

            using var stream = new MemoryStream();
            var offsets = new List<long> { 0 };
            WriteAscii(stream, "%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(stream.Position);
                WriteAscii(stream, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
            }

            var xref = stream.Position;
            WriteAscii(stream, $"xref\n0 {objects.Count + 1}\n");
            WriteAscii(stream, "0000000000 65535 f \n");
            for (var i = 1; i <= objects.Count; i++)
                WriteAscii(stream, $"{offsets[i]:D10} 00000 n \n");
            WriteAscii(stream, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
            return stream.ToArray();
        }

        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

        private static void WriteAscii(Stream stream, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
